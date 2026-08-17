using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;

namespace RavenNest.Blazor.Services.Knowledge
{
    /// <summary>
    ///     What the assistant knows, and the only thing that writes it.
    /// </summary>
    /// <remarks>
    ///     One JSON file, for the same reasons announcements are: this project has no EF migrations
    ///     and nothing reconciles the schema at startup, so a table is a hand run deployment step,
    ///     and GameData loads every set eagerly so a DbSet without a table takes the server down at
    ///     boot rather than on first use. Facts are also the right shape for a file. There are
    ///     hundreds rather than millions, nothing joins to them, and being able to read the whole
    ///     knowledge base in a text editor is worth a great deal while its shape is still being
    ///     learned.
    ///
    ///     <para>
    ///     Held in memory once read. Writes replace the file through a temporary file and a move, so
    ///     a crash part way through leaves the previous version rather than half of a new one.
    ///     </para>
    /// </remarks>
    public class FactService
    {
        private static readonly string FilePath =
            Path.Combine(FolderPaths.GeneratedDataPath, "facts.json");

        /// <summary>
        ///     How many facts a search hands back.
        /// </summary>
        /// <remarks>
        ///     Small on purpose. Every returned fact is tokens in the next request, and an
        ///     assistant given twelve loosely related paragraphs writes a worse answer than one
        ///     given three good ones.
        /// </remarks>
        private const int SearchLimit = 5;

        /// <summary>
        ///     Words that carry no meaning in a question and would otherwise match everything.
        /// </summary>
        /// <remarks>
        ///     Without this, questions match on their own scaffolding. "how do i tame a dragon"
        ///     scored a hit on "How do I join a clan?" purely for the word "how", which is how a
        ///     knowledge base ends up confidently answering a question nobody asked, and how a real
        ///     gap stops being recorded as a miss. Every word here is one a player types in almost
        ///     every question, so matching on it says nothing about relevance.
        /// </remarks>
        private static readonly HashSet<string> Ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "how", "what", "why", "when", "where", "who", "which", "does", "did", "was", "are",
            "the", "this", "that", "there", "here", "and", "but", "for", "with", "from", "into",
            "you", "your", "yours", "can", "could", "would", "should", "will", "shall", "may",
            "have", "has", "had", "get", "got", "make", "made", "any", "all", "some", "not",
            "about", "really", "actually", "hell", "please", "tell", "explain", "mean", "means",
            "work", "works", "working", "thing", "things", "stuff", "game", "ravenfall"
        };

        private readonly ILogger<FactService> logger;
        private readonly object mutex = new object();

        private List<Fact> facts;

        public FactService(ILogger<FactService> logger)
        {
            this.logger = logger;
        }

        /// <summary>Everything, for the admin panel.</summary>
        public IReadOnlyList<Fact> GetAll()
        {
            lock (mutex)
            {
                Load();
                return facts.OrderByDescending(x => x.UpdatedUtc ?? x.CreatedUtc).ToList();
            }
        }

        public IReadOnlyList<Fact> GetByStatus(FactStatus status)
        {
            lock (mutex)
            {
                Load();
                return facts.Where(x => x.Status == status)
                    .OrderByDescending(x => x.CreatedUtc)
                    .ToList();
            }
        }

        public Fact Get(Guid id)
        {
            lock (mutex)
            {
                Load();
                return facts.FirstOrDefault(x => x.Id == id);
            }
        }

        /// <summary>
        ///     What the assistant is allowed to find.
        /// </summary>
        /// <remarks>
        ///     Scored rather than filtered, so a query that matches nothing exactly still returns
        ///     its nearest thing rather than nothing at all. A title match counts for more than a
        ///     body match, because a title is written to be the question.
        ///
        ///     <para>
        ///     Keyword scoring rather than embeddings, and that is a sizing decision rather than a
        ///     preference: at this corpus size it is as good, and when it returns the wrong thing
        ///     you can see exactly why. The moment the misses log says otherwise, the shape here
        ///     does not have to change, only the scoring.
        ///     </para>
        /// </remarks>
        public IReadOnlyList<Fact> Search(string query, int limit = SearchLimit)
        {
            if (string.IsNullOrWhiteSpace(query)) return Array.Empty<Fact>();

            var words = query
                .Split(new[] { ' ', ',', '.', '?', '!', ':', ';', '\n', '\r', '\t' },
                       StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim().ToLowerInvariant())
                .Where(x => x.Length > 2 && !Ignored.Contains(x))
                .Distinct()
                .ToList();

            if (words.Count == 0) return Array.Empty<Fact>();

            lock (mutex)
            {
                Load();

                var scored = new List<(Fact fact, int score)>();

                foreach (var fact in facts)
                {
                    if (!fact.IsRetrievable) continue;

                    var title = (fact.Title ?? "").ToLowerInvariant();
                    var body = (fact.Body ?? "").ToLowerInvariant();
                    var tags = string.Join(" ", fact.Tags ?? new List<string>()).ToLowerInvariant();

                    var score = 0;
                    foreach (var word in words)
                    {
                        if (title.Contains(word)) score += 5;
                        if (tags.Contains(word)) score += 3;
                        if (body.Contains(word)) score += 1;
                    }

                    if (score > 0) scored.Add((fact, score));
                }

                var results = scored
                    .OrderByDescending(x => x.score)
                    .ThenByDescending(x => x.fact.TimesUsed)
                    .Take(limit)
                    .Select(x => x.fact)
                    .ToList();

                if (results.Count > 0)
                {
                    var now = DateTime.UtcNow;
                    foreach (var fact in results)
                    {
                        fact.TimesUsed++;
                        fact.LastUsedUtc = now;
                    }

                    Persist();
                }
                else
                {
                    // The misses are the backlog. A question nobody could answer is exactly the
                    // fact somebody should write, and this is the only place that knows.
                    RecordMiss(query);
                }

                return results;
            }
        }

        /// <summary>Queries that found nothing, newest first. The list of facts worth writing.</summary>
        public IReadOnlyList<KnowledgeMiss> GetMisses()
        {
            lock (mutex)
            {
                LoadMisses();
                return misses.OrderByDescending(x => x.LastAskedUtc).ToList();
            }
        }

        public void ClearMisses()
        {
            lock (mutex)
            {
                misses = new List<KnowledgeMiss>();
                PersistMisses();
            }
        }

        /// <summary>Creates or replaces a fact and returns what was stored.</summary>
        public Fact Save(Fact fact)
        {
            if (fact == null) throw new ArgumentNullException(nameof(fact));

            lock (mutex)
            {
                Load();

                var now = DateTime.UtcNow;
                if (fact.Id == Guid.Empty)
                {
                    fact.Id = Guid.NewGuid();
                    fact.CreatedUtc = now;
                }
                else
                {
                    fact.UpdatedUtc = now;
                }

                fact.Title = (fact.Title ?? "").Trim();
                fact.Body = (fact.Body ?? "").Trim();

                var existing = facts.FindIndex(x => x.Id == fact.Id);
                if (existing >= 0) facts[existing] = fact;
                else facts.Add(fact);

                Persist();
                return fact;
            }
        }

        /// <summary>
        ///     Accepts a proposal, so the assistant may use it.
        /// </summary>
        public bool Accept(Guid id, string acceptedBy)
        {
            lock (mutex)
            {
                Load();

                var fact = facts.FirstOrDefault(x => x.Id == id);
                if (fact == null) return false;

                fact.Status = FactStatus.Published;
                fact.AcceptedBy = acceptedBy;
                fact.AcceptedUtc = DateTime.UtcNow;

                // Superseding retires the old one here rather than at proposal time, so nothing is
                // lost while a proposal is still only a proposal.
                if (fact.Supersedes != null)
                {
                    var previous = facts.FirstOrDefault(x => x.Id == fact.Supersedes.Value);
                    if (previous != null) previous.Status = FactStatus.Retired;
                }

                Persist();
                return true;
            }
        }

        /// <summary>
        ///     Takes a fact out of use without losing it.
        /// </summary>
        /// <remarks>
        ///     Retiring rather than deleting, so a wrong answer somebody saw can still be traced to
        ///     what it said. Deleting is available for proposals, which nobody has seen.
        /// </remarks>
        public bool Retire(Guid id)
        {
            lock (mutex)
            {
                Load();
                var fact = facts.FirstOrDefault(x => x.Id == id);
                if (fact == null) return false;

                fact.Status = FactStatus.Retired;
                fact.UpdatedUtc = DateTime.UtcNow;
                Persist();
                return true;
            }
        }

        public bool Delete(Guid id)
        {
            lock (mutex)
            {
                Load();
                var removed = facts.RemoveAll(x => x.Id == id) > 0;
                if (removed) Persist();
                return removed;
            }
        }

        /// <summary>
        ///     Replaces every derived fact with a freshly generated set.
        /// </summary>
        /// <remarks>
        ///     Wholesale rather than merged, because a derived fact that no longer generates is one
        ///     whose rule was removed, and it should disappear rather than linger describing
        ///     something that is gone.
        /// </remarks>
        public void ReplaceDerived(IEnumerable<Fact> generated)
        {
            lock (mutex)
            {
                Load();

                facts.RemoveAll(x => x.Source == FactSource.Derived);

                foreach (var fact in generated)
                {
                    fact.Id = fact.Id == Guid.Empty ? Guid.NewGuid() : fact.Id;
                    fact.Source = FactSource.Derived;
                    fact.Status = FactStatus.Published;
                    fact.CreatedBy = "the code";
                    fact.CreatedUtc = DateTime.UtcNow;
                    facts.Add(fact);
                }

                Persist();
            }
        }

        /// <summary>
        ///     Replaces every imported wiki fact with a freshly fetched set.
        /// </summary>
        /// <remarks>
        ///     Wholesale, like the derived ones, so a page deleted from the wiki disappears here
        ///     rather than lingering as the only remaining copy of something its own authors
        ///     withdrew. The caller is responsible for not calling this with nothing: a wiki that is
        ///     down for an afternoon must not empty the knowledge base.
        /// </remarks>
        public void ReplaceFromWiki(IEnumerable<Fact> imported)
        {
            lock (mutex)
            {
                Load();

                facts.RemoveAll(x => x.Source == FactSource.Wiki);

                foreach (var fact in imported)
                {
                    fact.Id = fact.Id == Guid.Empty ? Guid.NewGuid() : fact.Id;
                    fact.Source = FactSource.Wiki;
                    fact.Status = FactStatus.Published;
                    facts.Add(fact);
                }

                Persist();
            }
        }

        /// <summary>
        ///     Marks a fact as describing something the code no longer says.
        /// </summary>
        public bool MarkStale(Guid id, string what)
        {
            lock (mutex)
            {
                Load();
                var fact = facts.FirstOrDefault(x => x.Id == id);
                if (fact == null || fact.Status != FactStatus.Published) return false;

                fact.Status = FactStatus.Stale;
                fact.Context = what;
                fact.UpdatedUtc = DateTime.UtcNow;
                Persist();
                return true;
            }
        }

        // ---- storage ---------------------------------------------------------------------------

        private void Load()
        {
            if (facts != null) return;

            facts = new List<Fact>();

            try
            {
                if (!File.Exists(FilePath)) return;

                var json = File.ReadAllText(FilePath);
                var loaded = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Fact>>(json);
                if (loaded != null) facts = loaded;
            }
            catch (Exception exc)
            {
                // An unreadable file must not take the site down, and nothing overwrites it until
                // somebody saves, so a bad file can still be recovered by hand.
                logger.LogError("Could not read facts from '" + FilePath + "': " + exc);
            }
        }

        private void Persist()
        {
            try
            {
                var folder = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                var json = Newtonsoft.Json.JsonConvert.SerializeObject(facts, Newtonsoft.Json.Formatting.Indented);

                // Through a temporary file and a move, so a crash part way through leaves the
                // previous version rather than half of a new one.
                var temp = FilePath + ".tmp";
                File.WriteAllText(temp, json);
                File.Move(temp, FilePath, true);
            }
            catch (Exception exc)
            {
                logger.LogError("Could not write facts to '" + FilePath + "': " + exc);
            }
        }

        // ---- misses ----------------------------------------------------------------------------

        private static readonly string MissesPath =
            Path.Combine(FolderPaths.GeneratedDataPath, "knowledge-misses.json");

        private List<KnowledgeMiss> misses;

        private void RecordMiss(string query)
        {
            LoadMisses();

            var text = query.Trim();
            var existing = misses.FirstOrDefault(x =>
                string.Equals(x.Query, text, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                existing.Times++;
                existing.LastAskedUtc = DateTime.UtcNow;
            }
            else
            {
                misses.Add(new KnowledgeMiss
                {
                    Query = text,
                    Times = 1,
                    LastAskedUtc = DateTime.UtcNow
                });
            }

            PersistMisses();
        }

        private void LoadMisses()
        {
            if (misses != null) return;

            misses = new List<KnowledgeMiss>();

            try
            {
                if (!File.Exists(MissesPath)) return;
                var json = File.ReadAllText(MissesPath);
                var loaded = Newtonsoft.Json.JsonConvert.DeserializeObject<List<KnowledgeMiss>>(json);
                if (loaded != null) misses = loaded;
            }
            catch (Exception exc)
            {
                logger.LogError("Could not read knowledge misses: " + exc);
            }
        }

        private void PersistMisses()
        {
            try
            {
                var folder = Path.GetDirectoryName(MissesPath);
                if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                var json = Newtonsoft.Json.JsonConvert.SerializeObject(misses, Newtonsoft.Json.Formatting.Indented);
                var temp = MissesPath + ".tmp";
                File.WriteAllText(temp, json);
                File.Move(temp, MissesPath, true);
            }
            catch (Exception exc)
            {
                logger.LogError("Could not write knowledge misses: " + exc);
            }
        }
    }

    /// <summary>
    ///     A question the knowledge base could not answer.
    /// </summary>
    /// <remarks>
    ///     This is the backlog, and it is the only honest source of one. What people actually ask
    ///     and get nothing for beats anybody's guess about what should be written down, and it is
    ///     also the evidence that decides whether keyword retrieval ever needs replacing.
    /// </remarks>
    public class KnowledgeMiss
    {
        public string Query { get; set; }
        public int Times { get; set; }
        public DateTime LastAskedUtc { get; set; }
    }
}
