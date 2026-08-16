using System;

namespace RavenNest.Blazor.Services.Announcements
{
    /// <summary>What kind of thing an announcement is, which decides how it is presented.</summary>
    public enum AnnouncementKind
    {
        /// <summary>Something happened, or something new exists.</summary>
        News,

        /// <summary>Something about the game has changed.</summary>
        Update,

        /// <summary>
        ///     Something is going to change and players need to know before it does. The one that
        ///     matters: a change to what things cost, or to what a player earns, cannot land as a
        ///     surprise or it reads as a bug.
        /// </summary>
        Notice,

        /// <summary>Something is broken or unavailable.</summary>
        Alert
    }

    /// <summary>
    ///     One post. Written in the admin panel, shown on the site, optionally pushed to Discord.
    /// </summary>
    public class Announcement
    {
        public Guid Id { get; set; }

        /// <summary>The part of the URL that names it, derived from the title.</summary>
        public string Slug { get; set; }

        public string Title { get; set; }

        /// <summary>
        ///     One or two sentences. This is what the front page card shows and what goes to
        ///     Discord, so it has to stand alone without the body.
        /// </summary>
        public string Summary { get; set; }

        /// <summary>Markdown.</summary>
        public string Body { get; set; }

        public string Author { get; set; }

        public AnnouncementKind Kind { get; set; }

        public DateTime CreatedUtc { get; set; }

        public DateTime? UpdatedUtc { get; set; }

        /// <summary>Null while it is a draft. Set when it goes live.</summary>
        public DateTime? PublishedUtc { get; set; }

        /// <summary>
        ///     When the thing being announced actually takes effect, for the posts that are about
        ///     something upcoming rather than something already done.
        ///
        ///     This is the field that lets the site say "this changes on Tuesday" rather than
        ///     leaving a reader to work out whether a post is a warning or a history note. A
        ///     change to the economy wants exactly that, in advance, in a place people look.
        /// </summary>
        public DateTime? EffectiveUtc { get; set; }

        /// <summary>Kept at the top of the list until it is unset.</summary>
        public bool Pinned { get; set; }

        /// <summary>Set once it has been pushed to Discord, so it is not sent twice.</summary>
        public DateTime? DiscordPostedUtc { get; set; }

        public bool IsPublished => PublishedUtc != null && PublishedUtc <= DateTime.UtcNow;

        /// <summary>True while the thing it describes has not happened yet.</summary>
        public bool IsUpcoming => EffectiveUtc != null && EffectiveUtc > DateTime.UtcNow;
    }
}
