using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    /// <summary>
    ///     A clan's public page as its owner set it up (the game's docs/clan-pages-design.md, steps 3 and 4): what the
    ///     public may see, the about text, the accent colour, the Top 8 and the visitor counter. A clan without a row
    ///     shows everything and has nothing written, so a row is only made when something changes.
    /// </summary>
    public partial class ClanPageSettings : Entity<ClanPageSettings>
    {
        [PersistentData] private Guid clanId;

        // what the public does not see (members of the clan always see everything); all shown by default
        [PersistentData] private bool hideMembers;
        [PersistentData] private bool hidePlaying;
        [PersistentData] private bool hideJoined;
        [PersistentData] private bool hideRanks;
        [PersistentData] private bool hideProgress;

        /// <summary>Plain text, up to 2000 characters, shown at the top of the page.</summary>
        [PersistentData] private string about;

        /// <summary>An admin hid the about text after a report; the clan sees that it was hidden.</summary>
        [PersistentData] private bool aboutHidden;

        /// <summary>#RRGGBB, or null for the site's gold.</summary>
        [PersistentData] private string accentColor;

        /// <summary>Up to eight character ids, comma separated, in the owner's order.</summary>
        [PersistentData] private string topMembers;

        [PersistentData] private long visitorCount;
        [PersistentData] private DateTime? updated;
    }

    /// <summary>A news post on a clan's page, for the public or for members only.</summary>
    public partial class ClanPost : Entity<ClanPost>
    {
        [PersistentData] private Guid clanId;
        [PersistentData] private Guid authorUserId;

        /// <summary>The character the author posted as, shown as the author; empty for the owner posting as themselves.</summary>
        [PersistentData] private Guid authorCharacterId;

        [PersistentData] private string title;
        [PersistentData] private string body;
        [PersistentData] private bool membersOnly;
        [PersistentData] private bool pinned;
        [PersistentData] private DateTime created;
        [PersistentData] private DateTime? edited;

        /// <summary>An admin hid it after a report; the clan still sees it, marked.</summary>
        [PersistentData] private bool hidden;
        [PersistentData] private bool deleted;
    }

    /// <summary>Someone reported a post, or a clan's about text (no post), for an admin to look at.</summary>
    public partial class ClanPostReport : Entity<ClanPostReport>
    {
        [PersistentData] private Guid clanId;

        /// <summary>The post, or empty for the about text.</summary>
        [PersistentData] private Guid postId;
        [PersistentData] private Guid reporterUserId;
        [PersistentData] private string reason;
        [PersistentData] private DateTime created;

        /// <summary>0 open, 1 hidden by an admin, 2 dismissed.</summary>
        [PersistentData] private int state;
        [PersistentData] private Guid? resolvedBy;
        [PersistentData] private DateTime? resolved;
    }
}
