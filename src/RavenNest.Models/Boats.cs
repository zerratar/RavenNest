using System;
namespace RavenNest.Models
{
    /// <summary>!boat build: a boat from the shipwright on Away, built from crafted parts and paid for in coins.</summary>
    public class BoatBuildRequest
    {
        public string Boat { get; set; }
    }

    public class BoatPart
    {
        public string Item { get; set; }
        public long Amount { get; set; }
        public long Have { get; set; }
    }

    /// <summary>What the shipwright builds, and what each boat asks for.</summary>
    public class BoatBlueprint
    {
        public string Boat { get; set; }
        public int Sailing { get; set; }
        public int Fishing { get; set; }
        public int Crafting { get; set; }
        public long Fee { get; set; }
        public BoatPart[] Parts { get; set; }
    }

    public class BoatBuildResult
    {
        public bool Ok { get; set; }

        /// <summary>Why not: notinsession, unknown (boat), island (not on Away), level, parts, coins, failed.</summary>
        public string Error { get; set; }
        public string Boat { get; set; }
        public string Island { get; set; }

        /// <summary>For level: the skill and the level it needs.</summary>
        public string Skill { get; set; }
        public int Level { get; set; }
        public long Fee { get; set; }

        /// <summary>The character's coins after building, or now when refused.</summary>
        public long Coins { get; set; }

        /// <summary>For parts: what is still missing.</summary>
        public BoatPart[] Missing { get; set; }

        /// <summary>What the shipwright builds (for unknown, and for !boat with nothing more).</summary>
        public BoatBlueprint[] Blueprints { get; set; }
    }

    /// <summary>One part of a boat's look: what it wears, how many there are and which of them the player owns.</summary>
    public class BoatLookSlot
    {
        public string Slot { get; set; }
        public int Count { get; set; }
        public bool CanBeNone { get; set; }
        public int Current { get; set; }
        public int[] Owned { get; set; }
    }

    /// <summary>!boat look: the equipped boat's look, or why it cannot be shown or changed.</summary>
    public class BoatLookResult
    {
        public bool Ok { get; set; }
        /// <summary>Why not: notinsession, noboat, slot, range, locked.</summary>
        public string Error { get; set; }
        public string Boat { get; set; }
        /// <summary>The look's recipe, as kept in the boat stack's Tag.</summary>
        public string Look { get; set; }
        public Guid InventoryItemId { get; set; }
        public BoatLookSlot[] Slots { get; set; }
    }
}
