using System;

namespace RavenNest.DataModels
{
    /// <summary>
    ///     An active effect on a character
    /// </summary>
    public class CharacterStatusEffect : Entity<CharacterStatusEffect>
    {
        private Guid characterId;
        private StatusEffectType type;
        private float amount;
        private DateTime startUtc;
        private DateTime expiresUtc;

        public Guid CharacterId { get => characterId; set => Set(ref characterId, value); }
        public StatusEffectType Type { get => type; set => Set(ref type, value); }
        public float Amount { get => amount; set => Set(ref amount, value); }
        public DateTime StartUtc { get => startUtc; set => Set(ref startUtc, value); }
        public DateTime ExpiresUtc { get => expiresUtc; set => Set(ref expiresUtc, value); }
    }
}
