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
        [Obsolete("Only kept for backward compatibility")] public DateTime StartUtc { get; set; }

        /// <summary>
        /// Gets the date when the effect will expire
        /// </summary>
        [Obsolete("Only kept for backward compatibility")] public DateTime ExpiresUtc { get; set; }

        /// <summary>
        /// Last time the effect was updated
        /// </summary>
        public DateTime LastUpdateUtc { get; set; }

        /// <summary>
        /// Gets the duration of the effect in seconds
        /// </summary>
        public double Duration { get; set; }

        /// <summary>
        /// Gets how many seconds left this effect will last
        /// </summary>
        public double TimeLeft { get; set; }
    }
}
