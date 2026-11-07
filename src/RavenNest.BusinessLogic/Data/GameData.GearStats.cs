using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace RavenNest.BusinessLogic.Data
{
    public partial class GameData
    {
        /// <summary>
        /// Themed combat v2 stats for gear that already exists (the game's docs/combat-v2-design.md, "Themed gear"),
        /// so elements and resistances can be felt before gems (phase 4) bring them to everything. Each set has one
        /// element: its weapons turn a share of their hits into it and its armour resists it.
        /// </summary>
        private static readonly (string Set, string Element)[] ThemedSets =
        {
            ("Dragon", "FIRE"),       // dragonfire
            ("Abraxas", "POISON"),    // the serpent's venom
            ("Phantom", "ICE"),       // the cold of the grave
            ("Ether", "LIGHTNING"),   // storms of the ether
        };

        private static readonly string[] SetWeapons = { "2H Sword", "Katana", "Sword", "2H Axe", "Axe", "Spear", "Bow", "Staff" };
        private static readonly string[] SetArmour = { "Helmet", "Chest", "Gloves", "Boots", "Leggings", "Shield" };

        /// <summary>Single pieces: jewellery named after gems and beasts, and the silly weapons (they deserve it).</summary>
        private static readonly Dictionary<string, string> ThemedPieces = new Dictionary<string, string>
        {
            ["Ruby Ring"] = "FIRE_RESISTANCE:10%",
            ["Ruby Amulet"] = "FIRE_RESISTANCE:10%",
            ["Emerald Ring"] = "POISON_RESISTANCE:10%",
            ["Emerald Amulet"] = "POISON_RESISTANCE:10%",
            ["Dragon Ring"] = "FIRE_DAMAGE:5%;FIRE_RESISTANCE:10%",
            ["Dragon Amulet"] = "FIRE_DAMAGE:5%;FIRE_RESISTANCE:10%",
            ["Phantom Ring"] = "ICE_DAMAGE:5%;ICE_RESISTANCE:10%",
            ["Phantom Amulet"] = "ICE_DAMAGE:5%;ICE_RESISTANCE:10%",
            ["Ring Of The Celestial"] = "LIGHTNING_RESISTANCE:15%;CRIT_CHANCE:2%",
            ["Windcallers Amulet"] = "LIGHTNING_DAMAGE:5%;EVASION:20",
            ["Owls Eye Ring"] = "CRIT_CHANCE:5%",
            ["Archmages Pendant"] = "CRIT_DAMAGE:15%",
            ["Knights Emblem"] = "MAX_HEALTH:150",
            ["Warriors Might Ring"] = "CRIT_DAMAGE:10%",

            ["Bacon 2H Sword"] = "FIRE_DAMAGE:30%",          // sizzling
            ["Turd 2H Sword"] = "POISON_DAMAGE:50%",         // you know why
            ["Ultimate Tuna Slapper"] = "ICE_DAMAGE:30%",    // straight from the freezer
            ["Pickle Rick"] = "POISON_DAMAGE:20%",           // brine
            ["Pickle Rick II"] = "POISON_DAMAGE:25%",
            ["Pickle Rick III"] = "POISON_DAMAGE:30%",
            ["Ghrims Stick"] = "LIGHTNING_DAMAGE:20%",
            ["Bananarang"] = "CRIT_CHANCE:10%",              // they always slip
        };

        /// <summary>
        /// Gives the themed gear its stats (<see cref="DataModels.Item.ExtraStats"/>) where an item has none yet, so a
        /// change made in the admin item editor is kept. Clear an item's stats there to get the defaults back.
        /// </summary>
        private void EnsureThemedGearStats()
        {
            var byName = new Dictionary<string, DataModels.Item>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var item in items.Entities)
            {
                if (!string.IsNullOrEmpty(item.Name)) byName[item.Name] = item;
            }

            var given = 0;
            void Give(string name, string stats)
            {
                if (!byName.TryGetValue(name, out var item) || !string.IsNullOrEmpty(item.ExtraStats)) return;
                item.ExtraStats = stats;
                given++;
            }

            foreach (var (set, element) in ThemedSets)
            {
                foreach (var weapon in SetWeapons)
                {
                    Give(set + " " + weapon, element + "_DAMAGE:25%");
                    Give("Elder " + set + " " + weapon, element + "_DAMAGE:35%");
                }
                foreach (var piece in SetArmour)
                {
                    Give(set + " " + piece, element + "_RESISTANCE:5%");
                    Give("Elder " + set + " " + piece, element + "_RESISTANCE:8%");
                }
            }

            foreach (var (name, stats) in ThemedPieces)
            {
                Give(name, stats);
            }

            if (given > 0) logger.LogInformation("Themed gear stats given to {Count} item(s).", given);

            // the skill trees' free respec (combat v2, docs/combat-skills-design.md): quests hand it out
            GetOrCreateItem(items.Entities, "Tome of Forgetting", "Gives back every skill tree point once, free of charge (!respec).", RavenNest.DataModels.ItemCategory.Scroll, RavenNest.DataModels.ItemType.Scroll);
        }
    }
}
