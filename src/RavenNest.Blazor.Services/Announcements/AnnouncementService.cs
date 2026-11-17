using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;

namespace RavenNest.Blazor.Services.Announcements
{
    /// <summary>
    ///     Reads and writes the site's announcements.
    /// </summary>
    /// <remarks>
    ///     Stored as one JSON file rather than a table, and that is a deliberate choice worth
    ///     explaining because everything else here lives in the database.
    ///
    ///     <para>
    ///     This project has no EF migrations and nothing that reconciles the schema at startup, so
    ///     a new table has to be created by hand on the server before any code that touches it can
    ///     ship, and GameData eagerly loads every entity set at boot, so a DbSet without a table
    ///     takes the server down on start rather than on first use. That is a real deployment step
    ///     for a feature that is a handful of posts a month.
    ///     </para>
    ///
    ///     <para>
    ///     Posts are also the wrong shape for that cost: there are few of them, they are appended
    ///     far more than edited, nothing joins to them, and losing one is an inconvenience rather
    ///     than a corruption. commands.json already sets the precedent for content read off disk.
    ///     If this ever needs to be a table, the model is already a plain object and the move is
    ///     small.
    ///     </para>
    ///
    ///     <para>
    ///     Held in memory once read. Writes replace the whole file through a temporary file and a
    ///     move, so a crash part way through leaves the previous version rather than half of a new
    ///     one.
    ///     </para>
    /// </remarks>
    public class AnnouncementService
    {
        private static readonly string FilePath =
            Path.Combine(FolderPaths.GeneratedDataPath, "announcements.json");

        /// <summary>The release news (<see cref="ReleaseNews"/>) that has gone in once, so a deleted post stays deleted.</summary>
        private static readonly string SeededPath =
            Path.Combine(FolderPaths.GeneratedDataPath, "announcements-seeded.json");

        private readonly ILogger<AnnouncementService> logger;
        private readonly GameData gameData;
        private readonly object mutex = new object();

        private List<Announcement> announcements;
        private HashSet<Guid> seeded;
        private DateTime nextSeedCheck;

        public AnnouncementService(ILogger<AnnouncementService> logger, GameData gameData)
        {
            this.logger = logger;
            this.gameData = gameData;
        }

        /// <summary>
        ///     Puts the release news that ships with the server on the site (<see cref="ReleaseNews"/>): each post once,
        ///     published and pinned as written, as soon as the game version it announces (or a newer one) is the one
        ///     the server expects. Called at startup and, at most once a minute, whenever the posts are read, so a post
        ///     appears within a minute of the release watcher seeing the new version. Returns how many went in.
        /// </summary>
        public int SeedReleaseNews()
        {
            lock (mutex)
            {
                Load();
                nextSeedCheck = DateTime.UtcNow.AddMinutes(1);
                if (loadFailed) return 0;
                var current = gameData?.Client != null ? gameData.GetClientVersion() : new Version();
                var added = 0;
                foreach (var release in ReleaseNews.Posts)
                {
                    if (seeded.Contains(release.Id) || current < release.Version) continue;
                    seeded.Add(release.Id);
                    if (announcements.Any(x => x.Id == release.Id)) continue;
                    var now = DateTime.UtcNow;
                    var source = release.Post;
                    var post = new Announcement
                    {
                        Id = release.Id,
                        Title = source.Title,
                        Summary = source.Summary,
                        Body = (source.Body ?? "").Replace("{top}", RavenNest.Combat.Tower.TopFloor.ToString()),
                        Author = source.Author,
                        Kind = source.Kind,
                        Pinned = release.Pinned,
                        CreatedUtc = now,
                        PublishedUtc = now,
                    };
                    post.Slug = UniqueSlug(post);
                    announcements.Add(post);
                    added++;
                    logger.LogInformation("Release news for " + release.Version + " is on the site: " + post.Title);
                }
                if (added > 0) Persist();
                PersistSeeded();
                return added;
            }
        }

        private void SeedWhenDue()
        {
            if (DateTime.UtcNow < nextSeedCheck) return;
            try { SeedReleaseNews(); }
            catch (Exception exc) { logger.LogError("Could not put the release news on the site: " + exc); }
        }

        /// <summary>Everything, newest first, drafts included. For the admin panel.</summary>
        public IReadOnlyList<Announcement> GetAll()
        {
            SeedWhenDue();
            lock (mutex)
            {
                Load();
                return Sorted(announcements);
            }
        }

        /// <summary>What the public should see, newest first, pinned posts before the rest.</summary>
        public IReadOnlyList<Announcement> GetPublished(int take = int.MaxValue)
        {
            SeedWhenDue();
            lock (mutex)
            {
                Load();
                return Sorted(announcements.Where(x => x.IsPublished)).Take(take).ToList();
            }
        }

        public Announcement GetBySlug(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug)) return null;

            lock (mutex)
            {
                Load();
                return announcements.FirstOrDefault(x =>
                    string.Equals(x.Slug, slug, StringComparison.OrdinalIgnoreCase));
            }
        }

        public Announcement GetById(Guid id)
        {
            lock (mutex)
            {
                Load();
                return announcements.FirstOrDefault(x => x.Id == id);
            }
        }

        /// <summary>Creates or replaces, and returns what was stored.</summary>
        public Announcement Save(Announcement post)
        {
            if (post == null) throw new ArgumentNullException(nameof(post));

            lock (mutex)
            {
                Load();

                var now = DateTime.UtcNow;
                if (post.Id == Guid.Empty)
                {
                    post.Id = Guid.NewGuid();
                    post.CreatedUtc = now;
                }
                else
                {
                    post.UpdatedUtc = now;
                }

                post.Title = (post.Title ?? "").Trim();
                post.Slug = UniqueSlug(post);

                var existing = announcements.FindIndex(x => x.Id == post.Id);
                if (existing >= 0) announcements[existing] = post;
                else announcements.Add(post);

                Persist();
                return post;
            }
        }

        public bool Delete(Guid id)
        {
            lock (mutex)
            {
                Load();
                var removed = announcements.RemoveAll(x => x.Id == id) > 0;
                if (removed) Persist();
                return removed;
            }
        }

        /// <summary>
        ///     Published posts that have not been pushed to Discord yet. Nothing sends these on its
        ///     own; the admin panel decides when, so a draft cannot escape by accident.
        /// </summary>
        public IReadOnlyList<Announcement> GetPendingDiscord()
        {
            lock (mutex)
            {
                Load();
                return announcements.Where(x => x.IsPublished && x.DiscordPostedUtc == null).ToList();
            }
        }

        public void MarkDiscordPosted(Guid id)
        {
            lock (mutex)
            {
                Load();
                var post = announcements.FirstOrDefault(x => x.Id == id);
                if (post == null) return;
                post.DiscordPostedUtc = DateTime.UtcNow;
                Persist();
            }
        }

        /// <summary>
        ///     Pinned first, then by when it was published, then by when it was written so drafts
        ///     have a stable order too.
        /// </summary>
        private static List<Announcement> Sorted(IEnumerable<Announcement> source)
        {
            return source
                .OrderByDescending(x => x.Pinned)
                .ThenByDescending(x => x.PublishedUtc ?? x.CreatedUtc)
                .ToList();
        }

        /// <summary>
        ///     A url safe name from the title, with a number appended if another post already has
        ///     it. Slugs are the address of a post, so two of them being the same would make one
        ///     unreachable.
        /// </summary>
        private string UniqueSlug(Announcement post)
        {
            var baseSlug = Slugify(post.Title);
            if (baseSlug.Length == 0) baseSlug = "post";

            var slug = baseSlug;
            var suffix = 2;
            while (announcements.Any(x => x.Id != post.Id &&
                                          string.Equals(x.Slug, slug, StringComparison.OrdinalIgnoreCase)))
            {
                slug = baseSlug + "-" + suffix++;
            }

            return slug;
        }

        public static string Slugify(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";

            var text = new StringBuilder();
            var lastWasDash = false;

            foreach (var c in value.Trim().ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c) && c < 128)
                {
                    text.Append(c);
                    lastWasDash = false;
                }
                else if (!lastWasDash && text.Length > 0)
                {
                    text.Append('-');
                    lastWasDash = true;
                }
            }

            return text.ToString().Trim('-');
        }

        private void Load()
        {
            if (announcements != null) return;

            announcements = new List<Announcement>();
            LoadSeeded();

            try
            {
                if (!File.Exists(FilePath)) return;

                var json = File.ReadAllText(FilePath);
                var loaded = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Announcement>>(json);
                if (loaded != null) announcements = loaded;
            }
            catch (Exception exc)
            {
                // An unreadable file must not take the site down. An empty list shows the empty
                // state, which is wrong but harmless, and nothing overwrites the file until
                // somebody saves.
                logger.LogError("Could not read announcements from '" + FilePath + "': " + exc);
                loadFailed = true;
            }
        }

        // true when the posts file could not be read: nothing writes it then (the release news waits too)
        private bool loadFailed;

        private void LoadSeeded()
        {
            seeded = new HashSet<Guid>();
            try
            {
                if (!File.Exists(SeededPath)) return;
                var loaded = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Guid>>(File.ReadAllText(SeededPath));
                if (loaded != null) seeded = new HashSet<Guid>(loaded);
            }
            catch (Exception exc)
            {
                logger.LogError("Could not read '" + SeededPath + "': " + exc);
                loadFailed = true;
            }
        }

        private void PersistSeeded()
        {
            try
            {
                var temp = SeededPath + ".tmp";
                File.WriteAllText(temp, Newtonsoft.Json.JsonConvert.SerializeObject(seeded.ToList()));
                File.Move(temp, SeededPath, overwrite: true);
            }
            catch (Exception exc)
            {
                logger.LogError("Could not write '" + SeededPath + "': " + exc);
            }
        }

        private void Persist()
        {
            try
            {
                var folder = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

                var json = Newtonsoft.Json.JsonConvert.SerializeObject(
                    announcements, Newtonsoft.Json.Formatting.Indented);

                // Written beside the real file and moved over it, so an interrupted write leaves
                // the previous version intact rather than a truncated one.
                var temp = FilePath + ".tmp";
                File.WriteAllText(temp, json);
                File.Move(temp, FilePath, overwrite: true);
            }
            catch (Exception exc)
            {
                logger.LogError("Could not write announcements to '" + FilePath + "': " + exc);
                throw;
            }
        }
    }
}
