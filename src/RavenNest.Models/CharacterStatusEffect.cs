using System;

namespace RavenNest.Models
{
    public class CharacterStatusEffect
    {
        /// <summary>
        /// The effect of the used item
        /// </summary>
        public StatusEffectType Type { get; set; }

        /// <summary>
        /// Gets the Strength of the effect
        /// </summary>
        public float Amount { get; set; }

        /// <summary>
        /// Gets the date when the effect was started
        /// </summary>
        public DateTime StartUtc { get; set; }

        /// <summary>
        /// Gets the date when the effect will expire
        /// </summary>
        public DateTime ExpiresUtc { get; set; }
    }
}
