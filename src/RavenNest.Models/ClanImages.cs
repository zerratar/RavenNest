using System;
using System.Collections.Generic;

namespace RavenNest.Models
{
    public enum ClanImageKind
    {
        Logo = 0,
        Cape = 1
    }

    public enum ClanImageState
    {
        /// <summary>In use.</summary>
        Live = 0,

        /// <summary>Waiting for an admin; the clan keeps its previous image meanwhile.</summary>
        Waiting = 1,

        /// <summary>Was live, a newer one took its place.</summary>
        Replaced = 2,

        /// <summary>An admin turned it down. The owner can try again.</summary>
        Rejected = 3,

        /// <summary>An admin blocked it: the file is gone and the owner's uploads wait for review.</summary>
        Blocked = 4,

        /// <summary>Was live or waiting, the owner went back to the default.</summary>
        Removed = 5
    }

    /// <summary>The cape shapes a clan can pick, in the order of the game's clan cape meshes.</summary>
    public enum ClanCapeStyle
    {
        Long = 0,
        Pointed = 1,
        Short = 2,
        Tattered = 3
    }

    /// <summary>
    ///     What a clan looks like: its logo and cape images and the shape of its capes. The game asks for it
    ///     (api/clan/look/{clanId}) and gets it again in the ClanLookChanged event.
    /// </summary>
    public class ClanLook
    {
        public Guid ClanId { get; set; }

        /// <summary>The live custom logo, empty for the owner's Twitch picture.</summary>
        public Guid? LogoImageId { get; set; }

        /// <summary>The live custom cape image, empty for the default (the logo on a cloth colour).</summary>
        public Guid? CapeImageId { get; set; }

        public int CapeStyle { get; set; }

        /// <summary>Roles whose cape shape differs from the clan's.</summary>
        public Dictionary<Guid, int> RoleCapeStyles { get; set; } = new Dictionary<Guid, int>();
    }

    public class ClanImageResult
    {
        public bool Ok { get; set; }

        /// <summary>Uploaded, but it waits for review before it is used.</summary>
        public bool Waiting { get; set; }

        public string Error { get; set; }
        public Guid? ImageId { get; set; }
    }

    /// <summary>One upload as the admin page and the clan page show it.</summary>
    public class ClanImageInfo
    {
        public Guid Id { get; set; }
        public Guid ClanId { get; set; }
        public Guid UserId { get; set; }
        public ClanImageKind Kind { get; set; }
        public ClanImageState State { get; set; }
        public DateTime Created { get; set; }
        public DateTime? Reviewed { get; set; }
        public string ReviewedBy { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Size { get; set; }
    }

    /// <summary>A row on the admin page: one uploader, their clan and what they have now.</summary>
    public class ClanImageUploader
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; }
        public Guid? ClanId { get; set; }
        public string ClanName { get; set; }
        public ClanImageInfo Logo { get; set; }
        public ClanImageInfo Cape { get; set; }
        public int WaitingCount { get; set; }
        public int Uploads { get; set; }
        public DateTime LastUpload { get; set; }
        public bool NeedsReview { get; set; }
    }

    public class ClanImagePage<T>
    {
        public List<T> Items { get; set; } = new List<T>();
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }
}
