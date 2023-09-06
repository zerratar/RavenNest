using System;
using System.Collections.Generic;

namespace RavenNest.Models
{
    public class ItemUseResult
    {
        /// <summary>
        /// The applied status effect after using the item
        /// </summary>
        public List<CharacterStatusEffect> Effects { get; set; }

        /// <summary>
        /// The target inventory item, if the item does not exist, add it.
        /// </summary>
        public Guid InventoryItemId { get; set; }

        /// <summary>
        /// If <= 0, remove the stack, otherwise replace the count with this.
        /// </summary>
        public int NewStackAmount { get; set; }

        /// <summary>
        ///     Whether or not this item use caused player to be teleported
        /// </summary>
        public bool Teleport { get; set; }

        /// <summary>
        /// If player is being teleported, this is the target island
        /// </summary>
        public Island EffectIsland { get; set; }
    }
}
