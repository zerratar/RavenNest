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

        /// <summary>
        /// What an item the game words itself did (v0.9.7, the Message in a Bottle and the Treasure Map): "note" (a note
        /// to read, <see cref="Note"/>), "map" (a Treasure Map came out, its island in <see cref="EffectIsland"/>),
        /// "elsewhere" (the map marks another island, <see cref="EffectIsland"/>), "locked" (the map's island is not
        /// open in this stream) or "treasure" (dug up: <see cref="Coins"/>, <see cref="Items"/>, <see cref="Look"/>).
        /// Null for every other item.
        /// </summary>
        public string Outcome { get; set; }

        /// <summary>The note in the bottle, by its number: the game has the words (Localization, UI_BOTTLE_NOTE_n).</summary>
        public int Note { get; set; }

        /// <summary>Coins found with a treasure. Already added on the server; the game only words it.</summary>
        public long Coins { get; set; }

        /// <summary>Items found with a treasure, already in the inventory (the server's inventory feed carries them).</summary>
        public List<EventItemReward> Items { get; set; }

        /// <summary>A boat look found with a treasure ("flag_8"), or null.</summary>
        public string Look { get; set; }
    }
}
