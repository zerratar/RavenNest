using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    /// <summary>
    ///     An active effect on a character
    /// </summary>
    public partial class CharacterStatusEffect : Entity<CharacterStatusEffect>
    {
        [PersistentData] private Guid characterId;
        [PersistentData] private StatusEffectType type;
        [PersistentData] private double amount;
        [PersistentData] private DateTime startUtc;
        [PersistentData] private DateTime expiresUtc;
        [PersistentData] private DateTime lastUpdateUtc;
        [PersistentData] private double timeLeft;
        [PersistentData] private double duration;

        //public Guid CharacterId { get => characterId; set => Set(ref characterId, value); }
        //public StatusEffectType Type { get => type; set => Set(ref type, value); }
        //public double Amount { get => amount; set => Set(ref amount, value); }
        //[Obsolete("Only kept for backward compatibility")] public DateTime StartUtc { get => startUtc; set => Set(ref startUtc, value); }
        //[Obsolete("Only kept for backward compatibility")] public DateTime ExpiresUtc { get => expiresUtc; set => Set(ref expiresUtc, value); }
        //public DateTime LastUpdateUtc { get => lastUpdateUtc; set => Set(ref lastUpdateUtc, value); }
        //public double Duration { get => duration; set => Set(ref duration, value); }
        //public double TimeLeft { get => timeLeft; set => Set(ref timeLeft, value); }
    }
}
