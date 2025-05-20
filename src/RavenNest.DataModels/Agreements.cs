using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    public partial class Agreements : Entity<Agreements>
    {
        [PersistentData] private string type;
        [PersistentData] private string title;
        [PersistentData] private string message;
        [PersistentData] private DateTime? validFrom;
        [PersistentData] private DateTime? validTo;
        [PersistentData] private DateTime? lastModified;
        [PersistentData] private int revision;
        [PersistentData] private bool visibleInClient;
    }
}
