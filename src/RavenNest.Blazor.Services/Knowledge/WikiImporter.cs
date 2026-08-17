using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace RavenNest.Blazor.Services.Knowledge
{
    /// <summary>
    ///     Brings the community wiki into the knowledge base.
    /// </summary>
    /// <remarks>
    ///     ravenfall.fandom.com, which is MediaWiki, so there is a proper API and none of this has
    ///     to scrape HTML.
    ///
    ///     <para>
    ///     It is small: thirty real articles once redirects are excluded, about 92 KB in total, and
    ///     one active editor. So this is one pass rather than a pipeline, and nothing here should be
    ///     designed on the assumption that the wiki grows or fixes itself.
    ///     </para>
    ///
    ///     <para>
    ///     It is also not ours and not authoritative. Every imported fact keeps its URL so an answer
    ///     can link to the page it came from, and a wrong answer is traceable to the wiki rather
    ///     than appearing to be something the site asserted.
    ///     </para>
    /// </remarks>
    public class WikiImporter
    {
        private const string Api = "https://ravenfall.fandom.com/api.php";
        private const string PageBase = "https://ravenfall.fandom.com/wiki/";

        /// <summary>
        ///     Above this, an article is split into one fact per section instead of stored whole.
        /// </summary>
        /// <remarks>
        ///     Measured against the stripped text, not the wikitext. Weapons is 24 KB raw, a quarter
        ///     of the whole wiki, but almost all of that is stat tables and galleries: it comes out
        ///     at under two thousand characters of actual prose and does not need splitting. What
        ///     does split is Enchanting, Commands and the setup guide, which are long because they
        ///     genuinely say a lot.
        ///
        ///     <para>
        ///     Losing the weapon stat tables is the right trade rather than a regret. The game's own
        ///     item data is authoritative and the assistant reads it directly through search_items,
        ///     so a hand maintained table on a wiki with one active editor is the worse of the two
        ///     sources and the one more likely to be out of date.
        ///     </para>
        /// </remarks>
        private const int SplitOverCharacters = 4000;

        /// <summary>
        ///     One client for the lifetime of the process. Fandom rejects a request with no user
        ///     agent, so it says who this is.
        /// </summary>
        private static readonly HttpClient Http = CreateClient();

        private readonly FactService facts;
        private readonly ILogger<WikiImporter> logger;

        public WikiImporter(FactService facts, ILogger<WikiImporter> logger)
        {
            this.facts = facts;
            this.logger = logger;
        }

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.Add("User-Agent", "RavenNest/1.0 (https://www.ravenfall.stream)");
            return client;
        }

        /// <summary>
        ///     Fetches every article and replaces the imported facts with what came back.
        /// </summary>
        /// <remarks>
        ///     Replaces rather than merges, for the same reason the derived facts do: a page deleted
        ///     from the wiki should disappear here rather than linger as the only remaining copy of
        ///     something its own authors withdrew.
        ///
        ///     <para>
        ///     Nothing is replaced unless the fetch produced something. A wiki that is down for an
        ///     afternoon must not empty the knowledge base.
        ///     </para>
        /// </remarks>
        public async Task<int> ImportAsync()
        {
            var titles = await FetchTitlesAsync();
            if (titles.Count == 0)
            {
                logger.LogWarning("The wiki returned no articles, so nothing was imported. " +
                                  "Anything imported before is left alone.");
                return 0;
            }

            var imported = new List<Fact>();

            foreach (var title in titles)
            {
                try
                {
                    var text = await FetchArticleAsync(title);
                    if (string.IsNullOrWhiteSpace(text)) continue;

                    imported.AddRange(ToFacts(title, text));
                }
                catch (Exception exc)
                {
                    // One bad page is not a reason to lose the other twenty nine.
                    logger.LogError("Could not import the wiki page '" + title + "': " + exc.Message);
                }
            }

            if (imported.Count == 0)
            {
                logger.LogWarning("Nothing usable came back from the wiki; the previous import is kept.");
                return 0;
            }

            facts.ReplaceFromWiki(imported);
            logger.LogInformation("Imported " + imported.Count + " facts from " + titles.Count + " wiki articles.");
            return imported.Count;
        }

        private async Task<List<string>> FetchTitlesAsync()
        {
            var titles = new List<string>();

            try
            {
                // Main namespace, no redirects. Half the pages are redirects, and importing both
                // Armor and Armour would double the corpus with duplicates.
                var url = Api + "?action=query&list=allpages&apnamespace=0&apfilterredir=nonredirects" +
                          "&aplimit=500&format=json";

                var json = await Http.GetStringAsync(url);
                foreach (Match match in Regex.Matches(json, "\"title\"\\s*:\\s*\"(.*?)\"") )
                {
                    var title = Unescape(match.Groups[1].Value);
                    if (!string.IsNullOrWhiteSpace(title)) titles.Add(title);
                }
            }
            catch (Exception exc)
            {
                logger.LogError("Could not list the wiki's articles: " + exc.Message);
            }

            return titles;
        }

        /// <summary>
        ///     The article's wikitext, with its markup taken off.
        /// </summary>
        /// <remarks>
        ///     prop=extracts would hand back clean prose and was the first thing tried, but Fandom
        ///     does not run the TextExtracts extension: it answers "Unrecognized value for parameter
        ///     prop" and returns a page with no content at all, which is why the first run imported
        ///     nothing. So the wikitext comes back raw and the markup comes off here.
        /// </remarks>
        private async Task<string> FetchArticleAsync(string title)
        {
            var url = Api + "?action=query&prop=revisions&rvprop=content&rvslots=main&format=json&titles=" +
                      WebUtility.UrlEncode(title);

            var json = await Http.GetStringAsync(url);

            // The content arrives under a property literally named "*".
            var match = Regex.Match(json, "\"\\*\"\\s*:\\s*\"(.*?)\"\\s*\\}", RegexOptions.Singleline);
            if (!match.Success) return null;

            return StripMarkup(Unescape(match.Groups[1].Value));
        }

        /// <summary>
        ///     Turns wikitext into something worth reading out.
        /// </summary>
        /// <remarks>
        ///     Imperfect by nature, and it does not need to be perfect. What matters is that no
        ///     markup survives to be read back as if it were prose, and that galleries and template
        ///     calls disappear rather than being mangled: they are a large part of what these pages
        ///     contain and none of what anybody is asking about.
        /// </remarks>
        private static string StripMarkup(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;

            // Galleries are lists of image file names, and several pages are mostly gallery.
            text = Regex.Replace(text, @"<gallery[^>]*>.*?</gallery>", "", RegexOptions.Singleline);
            text = Regex.Replace(text, @"<ref[^>]*>.*?</ref>", "", RegexOptions.Singleline);
            text = Regex.Replace(text, @"<!--.*?-->", "", RegexOptions.Singleline);
            text = Regex.Replace(text, @"</?[a-zA-Z][^>]*>", "");

            // Template calls. Their expansion is not in this response, so the raw call is noise.
            text = Regex.Replace(text, @"\{\{[^{}]*\}\}", "");
            text = Regex.Replace(text, @"\{\|.*?\|\}", "", RegexOptions.Singleline);

            // Links: keep the words a reader would have seen, drop the target.
            text = Regex.Replace(text, @"\[\[[^\]|]*\|([^\]]*)\]\]", "$1");
            text = Regex.Replace(text, @"\[\[([^\]]*)\]\]", "$1");
            text = Regex.Replace(text, @"\[https?://\S+\s+([^\]]*)\]", "$1");
            text = Regex.Replace(text, @"\[https?://\S+\]", "");

            // File and category lines carry nothing anybody asked for.
            text = Regex.Replace(text, @"(?m)^\s*(File|Image|Category)\s*:.*$", "");

            text = text.Replace("'''", "").Replace("''", "");

            // Bullets keep their meaning without their wiki markers.
            text = Regex.Replace(text, @"(?m)^[\*#:;]+\s*", "- ");

            // Tidy what all of that left behind.
            text = Regex.Replace(text, @"\n{3,}", "\n\n");
            text = Regex.Replace(text, @"[ \t]{2,}", " ");

            return text.Trim();
        }

        /// <summary>
        ///     Turns one article into one fact, or into one per section when it is long.
        /// </summary>
        private IEnumerable<Fact> ToFacts(string title, string text)
        {
            var url = PageBase + title.Replace(' ', '_');
            var now = DateTime.UtcNow;

            if (text.Length <= SplitOverCharacters)
            {
                yield return new Fact
                {
                    Title = title,
                    Body = Trim(text),
                    Tags = new List<string> { "wiki", title.ToLowerInvariant() },
                    Source = FactSource.Wiki,
                    Status = FactStatus.Published,
                    CreatedBy = "the community wiki",
                    CreatedUtc = now,
                    SourceUrl = url,
                    FetchedUtc = now
                };

                yield break;
            }

            foreach (var section in Split(text))
            {
                yield return new Fact
                {
                    // Both names, because somebody asks about weapon aim rather than about the
                    // Weapons article, and the section heading is the half that matches.
                    Title = title + ": " + section.Heading,
                    Body = Trim(section.Body),
                    Tags = new List<string> { "wiki", title.ToLowerInvariant(), section.Heading.ToLowerInvariant() },
                    Source = FactSource.Wiki,
                    Status = FactStatus.Published,
                    CreatedBy = "the community wiki",
                    CreatedUtc = now,

                    // Deep link, so the answer sends somebody to the right part of a long page.
                    SourceUrl = url + "#" + section.Heading.Replace(' ', '_'),
                    FetchedUtc = now
                };
            }
        }

        private readonly struct Section
        {
            public Section(string heading, string body) { Heading = heading; Body = body; }
            public string Heading { get; }
            public string Body { get; }
        }

        /// <summary>
        ///     Splits plain text extracts on their headings, which arrive as "== Heading ==".
        /// </summary>
        private static IEnumerable<Section> Split(string text)
        {
            var matches = Regex.Matches(text, @"^==+\s*(.+?)\s*==+\s*$", RegexOptions.Multiline);

            if (matches.Count == 0)
            {
                yield return new Section("Overview", text);
                yield break;
            }

            // Anything before the first heading is the lead, and it is often the best summary the
            // page has.
            var lead = text.Substring(0, matches[0].Index).Trim();
            if (lead.Length > 0) yield return new Section("Overview", lead);

            for (var i = 0; i < matches.Count; i++)
            {
                var start = matches[i].Index + matches[i].Length;
                var end = i + 1 < matches.Count ? matches[i + 1].Index : text.Length;

                var body = text.Substring(start, end - start).Trim();
                if (body.Length == 0) continue;

                yield return new Section(matches[i].Groups[1].Value, body);
            }
        }

        /// <summary>
        ///     Caps a section, so one enormous table cannot crowd out everything retrieved beside it.
        /// </summary>
        private static string Trim(string text)
        {
            const int max = 2000;
            text = text.Trim();
            if (text.Length <= max) return text;

            var cut = text.LastIndexOf('.', Math.Min(max, text.Length - 1));
            if (cut < max / 2) cut = max;

            return text.Substring(0, cut + 1).Trim() + "\n\n(Shortened. The full page is linked.)";
        }

        private static string Unescape(string value)
        {
            var text = new StringBuilder(value.Length);

            for (var i = 0; i < value.Length; i++)
            {
                if (value[i] != '\\' || i + 1 >= value.Length)
                {
                    text.Append(value[i]);
                    continue;
                }

                i++;
                switch (value[i])
                {
                    case 'n': text.Append('\n'); break;
                    case 't': text.Append('\t'); break;
                    case 'r': break;
                    case '"': text.Append('"'); break;
                    case '\\': text.Append('\\'); break;
                    case '/': text.Append('/'); break;
                    case 'u':
                        if (i + 4 < value.Length &&
                            int.TryParse(value.Substring(i + 1, 4),
                                         System.Globalization.NumberStyles.HexNumber,
                                         System.Globalization.CultureInfo.InvariantCulture,
                                         out var code))
                        {
                            text.Append((char)code);
                            i += 4;
                        }
                        break;
                    default: text.Append(value[i]); break;
                }
            }

            return text.ToString();
        }
    }
}
