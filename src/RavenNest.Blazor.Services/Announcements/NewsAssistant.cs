using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RavenNest.BusinessLogic.AI;

namespace RavenNest.Blazor.Services.Announcements
{
    public enum NewsAssistTask
    {
        Draft,
        Rewrite,
        Summarise,
        Title
    }

    /// <summary>
    ///     Writing help for the news editor.
    /// </summary>
    /// <remarks>
    ///     A thin thing on top of <see cref="IAiService"/>: it knows what an announcement is meant
    ///     to sound like, and nothing else. Everything it produces lands in a box next to the
    ///     editor for a person to read and copy across. Nothing it writes replaces what is already
    ///     typed, and nothing it writes is published by pressing a button in this class.
    ///
    ///     <para>
    ///     It gets one tool, and that tool only reads: the recent posts. It is what turns "write
    ///     about the vendor change" into something that knows the vendor change was already
    ///     mentioned in March, and it cannot alter anything.
    ///     </para>
    /// </remarks>
    public class NewsAssistant
    {
        private const string Voice = @"
You write announcements for Ravenfall, a game people play in a Twitch chat while a streamer runs it.

Who is reading: players. Some have been here for years, most have not read a patch note in their
life. They care about what changes for them, what it costs them, and when.

How to write:
- Plain words. Say what happened and what it means for the reader.
- Lead with the change, not with the reasoning. The reasoning can follow.
- Anything that affects what a player earns, pays, or can lose gets said outright, early, with
  the date it starts. Never bury it and never soften it.
- No hype, no exclamation marks, no marketing voice, no emoji.
- Never invent a detail. If a number, date, or name is not in what you were given, leave a clear
  gap like [how many] rather than guessing.
- Never use em dashes or en dashes. Use a comma, a full stop, or a plain hyphen.
- British-neutral spelling, matching the rest of the site.

Formatting: markdown. Short paragraphs. A list only when there is genuinely a list. No headings
above level two. Raw HTML is stripped before anybody sees it, so do not write any.
";

        private readonly IAiService ai;
        private readonly AnnouncementService announcements;

        public NewsAssistant(IAiService ai, AnnouncementService announcements)
        {
            this.ai = ai;
            this.announcements = announcements;
        }

        public bool IsAvailable => ai.IsConfigured;

        public string ModelInUse => ai.ModelInUse;

        public async Task<AiResult> AssistAsync(
            NewsAssistTask task,
            Announcement draft,
            string notes,
            CancellationToken cancellationToken = default)
        {
            if (!IsAvailable)
            {
                return AiResult.Failed("No OpenAI API key is set. Add one under Server settings.");
            }

            var request = new AiRequest
            {
                Instructions = Voice + "\n" + InstructionsFor(task),
                Input = Describe(task, draft, notes),
                Tools = new[] { RecentPostsTool() },
                MaxOutputTokens = task == NewsAssistTask.Title ? 400 : 4000
            };

            var result = await ai.AskAsync(request, cancellationToken);

            if (result.Ok)
            {
                result.Text = Clean(result.Text);
            }

            return result;
        }

        private static string InstructionsFor(NewsAssistTask task) => task switch
        {
            NewsAssistTask.Draft =>
                "Write the body of the post from the notes you are given. Markdown, no title line: " +
                "the title is a separate field. Return the body and nothing else, with no preamble " +
                "and no commentary about what you wrote.",

            NewsAssistTask.Rewrite =>
                "Rewrite the body you are given so it is clearer and shorter, keeping every fact, " +
                "number and date exactly as written. Do not add anything that is not already there. " +
                "Return the rewritten body and nothing else.",

            NewsAssistTask.Summarise =>
                "Write the summary line. Two sentences at most. It appears on a card with nothing " +
                "around it, so it has to stand on its own without the title. If the post is about " +
                "something changing, the summary says what and when. Return the summary and nothing else.",

            NewsAssistTask.Title =>
                "Suggest five titles, one per line, numbered. Each says what happened, in under " +
                "sixty characters. No colons splitting a clever half from a plain half, no questions.",

            _ => ""
        };

        private static string Describe(NewsAssistTask task, Announcement draft, string notes)
        {
            var text = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(draft?.Title))
            {
                text.AppendLine("Working title: " + draft.Title);
            }

            if (!string.IsNullOrWhiteSpace(draft?.Summary))
            {
                text.AppendLine("Current summary: " + draft.Summary);
            }

            if (draft?.EffectiveUtc != null)
            {
                // The date is the part a reader most needs and the part a model is most likely to
                // make up, so it is handed over rather than left to be inferred.
                text.AppendLine("This takes effect on " + draft.EffectiveUtc.Value.ToString("d MMMM yyyy") + ".");
            }

            if (draft != null)
            {
                text.AppendLine("It is filed as: " + draft.Kind + ".");
            }

            if (!string.IsNullOrWhiteSpace(draft?.Body))
            {
                text.AppendLine();
                text.AppendLine(task == NewsAssistTask.Rewrite ? "The body to rewrite:" : "What is written so far:");
                text.AppendLine(draft.Body);
            }

            if (!string.IsNullOrWhiteSpace(notes))
            {
                text.AppendLine();
                text.AppendLine("Notes from the person writing it:");
                text.AppendLine(notes);
            }

            return text.Length == 0
                ? "There is nothing written yet. Ask for what you would need to know."
                : text.ToString();
        }

        /// <summary>
        ///     The published posts, most recent first. Read only: it exists so a draft can refer to
        ///     what has already been said instead of announcing the same thing twice.
        /// </summary>
        private AiTool RecentPostsTool()
        {
            return new AiTool(
                "recent_announcements",
                "The announcements already published, newest first, with their dates and summaries. " +
                "Use this to avoid repeating something that has already been said, or to refer back to it.",
                AiTool.NoParameters,
                (arguments, cancellationToken) =>
                {
                    var posts = announcements.GetPublished(15)
                        .Select(x => new
                        {
                            title = x.Title,
                            published = x.PublishedUtc?.ToString("yyyy-MM-dd"),
                            takesEffect = x.EffectiveUtc?.ToString("yyyy-MM-dd"),
                            kind = x.Kind.ToString(),
                            summary = x.Summary
                        })
                        .ToList();

                    return Task.FromResult(posts.Count == 0
                        ? "Nothing has been published yet."
                        : JsonSerializer.Serialize(posts));
                });
        }

        /// <summary>
        ///     Takes the dashes out.
        /// </summary>
        /// <remarks>
        ///     The instructions ask for it and the model mostly complies, which is not the same as
        ///     always. Em and en dashes render as boxes in the game's font and read as machine
        ///     written on the site, and this is the one house rule cheap enough to simply enforce
        ///     rather than hope for.
        /// </remarks>
        private static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            return text
                .Replace(" — ", ", ")
                .Replace("—", "-")
                .Replace(" – ", ", ")
                .Replace("–", "-")
                .Trim();
        }
    }
}
