using System;
using System.Collections.Generic;

namespace RavenNest.Blazor.Services.Knowledge
{
    /// <summary>Where a fact came from, which decides how much it is trusted and who may edit it.</summary>
    public enum FactSource
    {
        /// <summary>Written by an administrator or moderator.</summary>
        Admin,

        /// <summary>Came out of a conversation, from a correction or from the assistant itself.</summary>
        Learned,

        /// <summary>Taken from the community wiki. Not ours, and attributed.</summary>
        Wiki,

        /// <summary>
        ///     Generated from the code at startup. Never edited by hand, because the next startup
        ///     would overwrite it.
        /// </summary>
        Derived
    }

    public enum FactStatus
    {
        /// <summary>Retrievable. The assistant may use it.</summary>
        Published,

        /// <summary>Waiting for somebody with authority to accept it. Invisible to the assistant.</summary>
        Proposed,

        /// <summary>Was published, is not any more. Kept so a wrong answer can be traced.</summary>
        Retired,

        /// <summary>
        ///     Published, but something in the code it described has since changed. Still
        ///     retrievable, and flagged for review.
        /// </summary>
        Stale
    }

    /// <summary>
    ///     One thing the assistant knows.
    /// </summary>
    /// <remarks>
    ///     Deliberately small. A fact answers one question in a paragraph or two, because retrieval
    ///     returns whole facts and a long one crowds out everything else it was found alongside.
    /// </remarks>
    public class Fact
    {
        public Guid Id { get; set; }

        /// <summary>What it answers, in a line. This is what retrieval matches against first.</summary>
        public string Title { get; set; }

        /// <summary>The answer. Markdown, kept short.</summary>
        public string Body { get; set; }

        public List<string> Tags { get; set; } = new List<string>();

        public FactSource Source { get; set; }

        public FactStatus Status { get; set; }

        /// <summary>A user name, or "assistant" when it wrote the proposal itself.</summary>
        public string CreatedBy { get; set; }

        public DateTime CreatedUtc { get; set; }

        public DateTime? UpdatedUtc { get; set; }

        /// <summary>Who accepted it, and when. Null while it is only proposed.</summary>
        public string AcceptedBy { get; set; }

        public DateTime? AcceptedUtc { get; set; }

        /// <summary>
        ///     The fact this replaced, when it replaced one.
        /// </summary>
        /// <remarks>
        ///     Superseding rather than editing in place, so a wrong answer that reached players can
        ///     be traced back to when it was introduced and what it said before.
        /// </remarks>
        public Guid? Supersedes { get; set; }

        /// <summary>
        ///     What the assistant was asked when this proposal came out of a conversation. Without
        ///     it, a reviewer is judging an assertion with no idea what prompted it.
        /// </summary>
        public string Context { get; set; }

        /// <summary>Where a wiki fact came from, so an answer can link to the real page.</summary>
        public string SourceUrl { get; set; }

        /// <summary>When a wiki fact was fetched, so staleness is visible.</summary>
        public DateTime? FetchedUtc { get; set; }

        /// <summary>
        ///     Values from the code this wording depends on, and what they were when it was written.
        /// </summary>
        /// <remarks>
        ///     Checked at startup. A mismatch does not rewrite the fact, because no machine can do
        ///     that safely, it marks it stale and says what changed. This is what stops hand written
        ///     prose quietly describing a rule that was changed a year ago.
        /// </remarks>
        public Dictionary<string, string> DependsOn { get; set; } = new Dictionary<string, string>();

        /// <summary>How often retrieval has returned this. The load bearing ones are worth arguing about.</summary>
        public int TimesUsed { get; set; }

        public DateTime? LastUsedUtc { get; set; }

        /// <summary>Derived facts are regenerated every startup, so editing one would be undone.</summary>
        public bool IsEditable => Source != FactSource.Derived;

        public bool IsRetrievable => Status == FactStatus.Published || Status == FactStatus.Stale;
    }
}
