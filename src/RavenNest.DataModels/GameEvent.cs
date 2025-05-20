using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    public partial class GameEvent : Entity<GameEvent>
    {
        [PersistentData] private Guid gameSessionId;
        [PersistentData] private Guid userId;
        [PersistentData] private int type;
        [PersistentData] private int revision;
        [PersistentData] private byte[] data;
    }
}
