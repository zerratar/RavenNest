using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    /// <summary>
    ///     One clan logo or cape image a clan owner uploaded, and what became of it.
    /// </summary>
    /// <remarks>
    ///     The image itself is a file named after the id (ClanImageManager.FolderPath). A blocked image's
    ///     file is deleted, its row stays: the admin page shows that something was blocked, and when.
    /// </remarks>
    public partial class ClanImage : Entity<ClanImage>
    {
        [PersistentData] private Guid clanId;
        [PersistentData] private Guid userId;

        /// <summary>RavenNest.Models.ClanImageKind: 0 logo, 1 cape.</summary>
        [PersistentData] private int kind;

        /// <summary>RavenNest.Models.ClanImageState.</summary>
        [PersistentData] private int state;

        [PersistentData] private DateTime created;
        [PersistentData] private DateTime? reviewed;
        [PersistentData] private Guid? reviewedBy;
        [PersistentData] private string hash;
        [PersistentData] private int width;
        [PersistentData] private int height;
        [PersistentData] private int size;
    }
}
