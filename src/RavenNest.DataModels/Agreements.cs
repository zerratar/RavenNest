using System;

namespace RavenNest.DataModels
{
    public partial class Agreements : Entity<Agreements>
    {
        private string type; public string Type { get => type; set => Set(ref type, value); }
        private string title; public string Title { get => title; set => Set(ref title, value); }
        private string message; public string Message { get => message; set => Set(ref message, value); }
        private DateTime? validFrom; public DateTime? ValidFrom { get => validFrom; set => Set(ref validFrom, value); }
        private DateTime? validTo; public DateTime? ValidTo { get => validTo; set => Set(ref validTo, value); }
        private DateTime? lastModified; public DateTime? LastModified { get => lastModified; set => Set(ref lastModified, value); }
        private int revision; public int Revision { get => revision; set => Set(ref revision, value); }
        private bool visibleInClient; public bool VisibleInClient { get => visibleInClient; set => Set(ref visibleInClient, value); }
    }

    public enum Patreon : int
    {
        None = 0,
        Mithril = 1,
        Rune = 2,
        Dragon = 3,
        Abraxas = 4,
        Phantom = 5
    }
}
