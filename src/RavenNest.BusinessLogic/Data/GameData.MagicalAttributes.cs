using System;
using System.Collections.Generic;
using System.Linq;
using RavenNest.DataModels;
using RavenNest.Models;
using ItemType = RavenNest.Models.ItemType;
using ItemCategory = RavenNest.Models.ItemCategory;
using ItemAttribute = RavenNest.DataModels.ItemAttribute;

namespace RavenNest.BusinessLogic.Data
{
    public partial class GameData
    {
        /// <summary>
        /// Combat v2's magical enchantment attributes (the game's docs/combat-v2-design.md, "Magical attributes"): named
        /// like the gear stats (RavenNest.Combat.GearStats), so the game reads an enchantment of CRIT_CHANCE:3% the way
        /// it reads the stat on an item. Each fits some gear only (<see cref="MagicalAttributeFits"/>): what hits gets
        /// the offensive ones, what is worn the defensive ones. Games before 0.9.7 ignore names they do not know.
        /// </summary>
        private static readonly (string Name, string Description, string Min, string Max)[] MagicalAttributes =
        {
            ("CRIT_CHANCE", "Raises the chance of a critical hit", "1%", "5%"),
            ("CRIT_DAMAGE", "Makes critical hits hit harder", "2%", "25%"),
            ("ATTACK_SPEED", "Attacks more often", "1%", "8%"),
            ("ARMOR_PENETRATION", "Blows ignore part of the target's armour", "2%", "15%"),
            ("LIFE_STEAL", "Heals you by part of the damage you deal", "1%", "5%"),
            ("THORNS", "Hurts melee attackers by part of the damage they deal you", "2%", "15%"),
            ("THREAT", "Enemies pay you more attention (for tanks)", "5%", "30%"),
            ("DAMAGE_REDUCTION", "Takes part off all damage you take", "1%", "6%"),
        };

        private static readonly HashSet<string> Offensive = new HashSet<string> { "CRIT_CHANCE", "CRIT_DAMAGE", "ATTACK_SPEED", "ARMOR_PENETRATION", "LIFE_STEAL" };
        private static readonly HashSet<string> Defensive = new HashSet<string> { "THORNS", "THREAT", "DAMAGE_REDUCTION" };

        /// <summary>
        /// Whether a magical attribute may roll on an item: the offensive ones on weapons, rings and amulets (attack
        /// speed on gloves too), the defensive ones on armour and shields (threat on shields, helmets and chests only).
        /// Attributes that are not magical (skills, AIM, POWER, ARMOR) always fit here; their own checks stand.
        /// </summary>
        public static bool MagicalAttributeFits(string attribute, ItemCategory category, ItemType type)
        {
            if (attribute == null) return true;
            var weaponOrJewel = category == ItemCategory.Weapon || category == ItemCategory.Ring || category == ItemCategory.Amulet;
            if (attribute == "ATTACK_SPEED") return weaponOrJewel || type == ItemType.Gloves;
            if (Offensive.Contains(attribute)) return weaponOrJewel;
            if (attribute == "THREAT") return type == ItemType.Shield || type == ItemType.Helmet || type == ItemType.Chest;
            if (Defensive.Contains(attribute)) return category == ItemCategory.Armor;
            return true;
        }

        private void EnsureMagicalGearAttributes(IReadOnlyList<ItemAttribute> attributes)
        {
            var nextIndex = attributes.Count == 0 ? 0 : attributes.Max(x => x.AttributeIndex) + 1;
            foreach (var (name, description, min, max) in MagicalAttributes)
            {
                if (attributes.Any(x => x.Name == name)) continue;
                Add(new ItemAttribute
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    Description = description + " (up to " + max + ")",
                    AttributeIndex = nextIndex++,
                    DefaultValue = min,
                    MinValue = min,
                    MaxValue = max,
                    Type = 1
                });
            }

            // aim is accuracy in combat v2; the stored name stays AIM for 0.9.6 games
            var aim = attributes.FirstOrDefault(x => x.Name == "AIM");
            if (aim != null && aim.Description != null && aim.Description.Contains("Aim")) aim.Description = aim.Description.Replace("Aim", "Accuracy");
        }
    }
}
