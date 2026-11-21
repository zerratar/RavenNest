using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using RavenNest.Combat;
using RavenNest.DataModels;
using HeadMask = RavenNest.Models.HeadMask;
using ItemLine = RavenNest.Models.ItemLine;
using ItemLines = RavenNest.Models.ItemLines;

namespace RavenNest.BusinessLogic.Data
{
    /// <summary>
    ///     Class gear (v0.9.7, the game's docs/class-gear-design.md): five armor lines of eight tiers, six pieces each.
    ///     Everything about them is made here at startup from two tables: <see cref="ClassGearPieces"/> (what a piece is
    ///     and how it looks) and <see cref="ClassGearLines"/> (what a line gives). The code is the record for these items:
    ///     a row that differs is put right at the next start. Adding a line or a piece is adding rows.
    /// </summary>
    public partial class GameData
    {
        private sealed class ClassGearPiece
        {
            public ItemLine Line;
            /// <summary>Stays the same when the name, the type or the look changes: the item's id is made from it.</summary>
            public string Key;
            public ItemType Type;
            /// <summary>The name after the tier's: "Robe" makes "Adept Robe".</summary>
            public string Name;
            /// <summary>The parts on the character, as the game's part indices: the main part, then the additional ones.</summary>
            public string MaleModels;
            public string FemaleModels;
            /// <summary>What a head piece hides, or what a body piece leaves off.</summary>
            public HeadMask Mask;
        }

        private static ClassGearPiece Piece(ItemLine line, string key, ItemType type, string name, string models, HeadMask mask = HeadMask.None, string femaleModels = null)
        {
            return new ClassGearPiece { Line = line, Key = key, Type = type, Name = name, MaleModels = models, FemaleModels = femaleModels ?? models, Mask = mask };
        }

        /// <summary>
        ///     The pieces of every line. The model ids must be valid part indices in games before 0.9.7 too (they read the
        ///     same list and never look at the mask), so "no part" is said with a mask flag and a harmless index.
        ///     The two head pieces of a line have the same stats: they are a choice of look.
        /// </summary>
        private static readonly ClassGearPiece[] ClassGearPieces =
        {
            //    line            key         type                   name          model ids      mask
            Piece(ItemLine.Mage, "head1", ItemType.Hat, "Wizard Hat", "1"),
            Piece(ItemLine.Mage, "head2", ItemType.HeadCovering, "Hood", "6", HeadMask.Hair | HeadMask.Ears),
            Piece(ItemLine.Mage, "chest", ItemType.Chest, "Robe", "27,20,20,0", HeadMask.NoShoulderPads | HeadMask.NoElbowPads),
            Piece(ItemLine.Mage, "gloves", ItemType.Gloves, "Gloves", "9,18"),
            Piece(ItemLine.Mage, "leggings", ItemType.Leggings, "Robe Skirt", "9"),
            Piece(ItemLine.Mage, "boots", ItemType.Boots, "Boots", "11"),

            Piece(ItemLine.Healer, "head1", ItemType.HeadCovering, "Cowl", "4", HeadMask.Hair | HeadMask.Ears),
            Piece(ItemLine.Healer, "head2", ItemType.HeadCovering, "Cap", "0", HeadMask.Hair | HeadMask.Ears),
            Piece(ItemLine.Healer, "chest", ItemType.Chest, "Vestment", "10,20,20,0", HeadMask.NoShoulderPads | HeadMask.NoElbowPads),
            Piece(ItemLine.Healer, "gloves", ItemType.Gloves, "Gloves", "13,18"),
            Piece(ItemLine.Healer, "leggings", ItemType.Leggings, "Tabard", "8"),
            Piece(ItemLine.Healer, "boots", ItemType.Boots, "Boots", "11"),

            Piece(ItemLine.Ranger, "head1", ItemType.Hat, "Cap", "4"),
            Piece(ItemLine.Ranger, "head2", ItemType.HeadCovering, "Hood", "5", HeadMask.Hair | HeadMask.Ears), // 12 left the nape bare and hung a strap over the beard
            Piece(ItemLine.Ranger, "chest", ItemType.Chest, "Jerkin", "14,20,10,0", HeadMask.NoShoulderPads | HeadMask.NoElbowPads),
            Piece(ItemLine.Ranger, "gloves", ItemType.Gloves, "Gloves", "3,3"),
            Piece(ItemLine.Ranger, "leggings", ItemType.Leggings, "Trousers", "6"),
            Piece(ItemLine.Ranger, "boots", ItemType.Boots, "Boots", "19"),

            Piece(ItemLine.Melee, "head1", ItemType.HeadCovering, "Helm", "1", HeadMask.Hair | HeadMask.Ears),
            Piece(ItemLine.Melee, "head2", ItemType.HeadCovering, "Coif", "11", HeadMask.Hair | HeadMask.Ears),
            Piece(ItemLine.Melee, "chest", ItemType.Chest, "Hauberk", "17,13,19,0", HeadMask.NoElbowPads),
            Piece(ItemLine.Melee, "gloves", ItemType.Gloves, "Gauntlets", "5,17"),
            Piece(ItemLine.Melee, "leggings", ItemType.Leggings, "Tassets", "10"),
            Piece(ItemLine.Melee, "boots", ItemType.Boots, "Boots", "4"),

            Piece(ItemLine.Tank, "head1", ItemType.Helmet, "Greathelm", "6", HeadMask.All),
            Piece(ItemLine.Tank, "head2", ItemType.Helmet, "Visor Helm", "10", HeadMask.All),
            Piece(ItemLine.Tank, "chest", ItemType.Chest, "Cuirass", "20,10,14,3"),
            Piece(ItemLine.Tank, "gloves", ItemType.Gloves, "Gauntlets", "12,4"),
            Piece(ItemLine.Tank, "leggings", ItemType.Leggings, "Greaves", "20,6"),
            Piece(ItemLine.Tank, "boots", ItemType.Boots, "Sabatons", "13"),
        };

        private enum ClassGearSkill { Magic, Ranged, Attack, Defense }

        private sealed class ClassGearLineInfo
        {
            public ItemLine Line;
            /// <summary>The skill whose level a piece asks for (Healing passes a Magic requirement in the game and here).</summary>
            public ClassGearSkill Requires;
            /// <summary>One line saying whom it is for.</summary>
            public string Description;
            /// <summary>
            ///     What a full set of five gives, in percent, at tier 1 and at tier 8 (linear between, in halves).
            ///     Evasion is a rating, not a share: see <see cref="ClassGearEvasion"/>.
            /// </summary>
            public (GearStat Stat, double From, double To)[] Stats;
        }

        private static readonly ClassGearLineInfo[] ClassGearLines =
        {
            new ClassGearLineInfo
            {
                Line = ItemLine.Mage, Requires = ClassGearSkill.Magic,
                Description = "Cloth for those who train Magic: far less armor than plate, but more magic power, faster casts and better accuracy.",
                Stats = new[] { (GearStat.MagicPowerBonus, 2.0, 16.0), (GearStat.CastSpeed, 1.0, 8.0), (GearStat.AccuracyBonus, 1.0, 8.0) },
            },
            new ClassGearLineInfo
            {
                Line = ItemLine.Healer, Requires = ClassGearSkill.Magic,
                Description = "Cloth for those who train Healing: far less armor than plate, but stronger heals that also shield, shorter cooldowns and more health.",
                Stats = new[] { (GearStat.HealingPowerBonus, 2.0, 16.0), (GearStat.HealShield, 1.0, 8.0), (GearStat.CooldownReduction, 1.0, 6.0), (GearStat.MaxHealthBonus, 1.0, 5.0) },
            },
            new ClassGearLineInfo
            {
                Line = ItemLine.Ranger, Requires = ClassGearSkill.Ranged,
                Description = "Leather for those who train Ranged: less armor than plate, but more ranged power, critical hits, attack speed and evasion.",
                Stats = new[] { (GearStat.RangedPowerBonus, 2.0, 14.0), (GearStat.CritChance, 0.5, 4.0), (GearStat.AttackSpeed, 1.0, 6.0), (GearStat.Evasion, 1.0, 6.0) },
            },
            new ClassGearLineInfo
            {
                Line = ItemLine.Melee, Requires = ClassGearSkill.Attack,
                Description = "Mail for those who train Attack or Strength: a little less armor than plate, but more melee power, critical damage, attack speed and armor penetration.",
                Stats = new[] { (GearStat.MeleePowerBonus, 2.0, 14.0), (GearStat.CritDamage, 3.0, 20.0), (GearStat.AttackSpeed, 1.0, 5.0), (GearStat.ArmorPenetration, 2.0, 12.0) },
            },
            new ClassGearLineInfo
            {
                Line = ItemLine.Tank, Requires = ClassGearSkill.Defense,
                Description = "Heavy plate for those who train Defense and stand in front: the armor of plate, with more health, more threat and less damage taken.",
                Stats = new[] { (GearStat.MaxHealthBonus, 2.0, 12.0), (GearStat.Threat, 5.0, 30.0), (GearStat.DamageReduction, 1.0, 5.0) },
            },
        };

        // what a piece costs to make (the spec's recipes): cloth or leather per tier and a bar of trim, or bars and a strap of leather per tier
        private const int ClassGearChestCloth = 4, ClassGearLeggingsCloth = 3, ClassGearOtherCloth = 2;
        private const int ClassGearChestBars = 4, ClassGearLeggingsBars = 3, ClassGearOtherBars = 2;
        private const int ClassGearTankChestBars = 6, ClassGearTankLeggingsBars = 5, ClassGearTankOtherBars = 3;

        /// <summary>The item id of a class gear piece: fixed, so it is the same in every database and survives a rename.</summary>
        public static Guid ClassGearItemId(ItemLine line, int tier, string key)
        {
            byte[] hash;
            using (var md5 = MD5.Create()) hash = md5.ComputeHash(Encoding.UTF8.GetBytes("ravenfall-class-gear-" + line.ToString().ToLowerInvariant() + "-" + tier + "-" + key));
            hash[6] = (byte)((hash[6] & 0x0F) | 0x30);
            hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
            return new Guid(hash);
        }

        /// <summary>A set's total of a stat at a tier, in percent: linear from tier 1 to tier 8, in halves.</summary>
        internal static decimal ClassGearSetTotal(double from, double to, int tier)
        {
            var raw = (decimal)from + ((decimal)to - (decimal)from) * (tier - 1) / (ItemLines.Tiers - 1);
            return Math.Round(raw * 2, MidpointRounding.AwayFromZero) / 2;
        }

        /// <summary>
        ///     Evasion is a rating in this game (2 per Defense level plus gear), not a share. A set's total is what takes
        ///     about that many points off the hit chance of an enemy of the tier's level: 4 x level x the share.
        /// </summary>
        internal static int ClassGearEvasion(decimal percent, int level) => Math.Max(1, (int)Math.Round(percent / 100m * 4 * level, MidpointRounding.AwayFromZero));

        /// <summary>
        ///     One piece's share of a set total (in whole units): 15% each for head, gloves and boots, 25% for the leggings
        ///     and the chest takes what is left (about 30%), so the five pieces add up to the total exactly.
        /// </summary>
        internal static int ClassGearShare(int total, ItemType type)
        {
            var other = (total * 15 + 49) / 100;   // a half goes down
            var leggings = (total * 25 + 50) / 100; // a half goes up
            switch (type)
            {
                case ItemType.Chest: return total - leggings - 3 * other;
                case ItemType.Leggings: return leggings;
                default: return other;
            }
        }

        /// <summary>The stats of a piece beyond its armor, in the text form RavenNest.Combat.GearStats reads.</summary>
        internal static string ClassGearStats(ItemLine line, int tier, ItemType type)
        {
            var info = ClassGearLines.First(x => x.Line == line);
            var level = ItemLines.TierLevels[tier - 1];
            var stats = new RavenNest.Combat.GearStats();
            foreach (var (stat, from, to) in info.Stats)
            {
                var percent = ClassGearSetTotal(from, to, tier);
                if (RavenNest.Combat.GearStats.IsShare(stat))
                {
                    // shares in tenths of a percent, so no piece says "0.37%"
                    var tenths = ClassGearShare((int)(percent * 10), type);
                    stats[stat] = (double)(tenths / 1000m);
                }
                else
                {
                    stats[stat] = ClassGearShare(ClassGearEvasion(percent, level), type);
                }
            }
            return stats.ToString();
        }

        private static ItemType ClassGearPlateSlot(ItemType type)
        {
            switch (type)
            {
                case ItemType.Chest:
                case ItemType.Gloves:
                case ItemType.Leggings:
                case ItemType.Boots:
                    return type;
                default:
                    return ItemType.Helmet; // hats, hoods and helms all count as the helmet
            }
        }

        /// <summary>
        ///     The armor a plate piece of a slot has at a required Defense level, from the metal sets as they are now:
        ///     the set of that level (the better one where two share a level, as Bronze and Iron do), or a point on the
        ///     line between the two sets around it.
        /// </summary>
        private static double PlateArmorAt(IReadOnlyList<ItemSet> plate, ItemType slot, int level)
        {
            var points = new SortedDictionary<int, int>();
            foreach (var set in plate)
            {
                Item piece;
                switch (slot)
                {
                    case ItemType.Chest: piece = set.Chest; break;
                    case ItemType.Gloves: piece = set.Gloves; break;
                    case ItemType.Leggings: piece = set.Leggings; break;
                    case ItemType.Boots: piece = set.Boots; break;
                    default: piece = set.Helmet; break;
                }
                if (piece == null || piece.ArmorPower <= 0) continue;
                var required = Math.Max(1, piece.RequiredDefenseLevel);
                if (!points.TryGetValue(required, out var armor) || piece.ArmorPower > armor) points[required] = piece.ArmorPower;
            }

            if (points.Count == 0) return 0;
            var below = points.First();
            foreach (var point in points)
            {
                if (point.Key == level) return point.Value;
                if (point.Key > level)
                {
                    if (below.Key >= point.Key) return point.Value; // below the first set
                    return below.Value + (point.Value - below.Value) * (double)(level - below.Key) / (point.Key - below.Key);
                }
                below = point;
            }
            return below.Value; // above the last set
        }

        private void EnsureClassGear()
        {
            var known = GetKnownItems();
            var farming = RavenNest.Models.Skill.Farming;
            var slayer = RavenNest.Models.Skill.Slayer;

            // the two cloths: flax is farmed and woven into linen, hide comes from fighting creatures and is tanned into
            // leather. One linen and one leather for every tier: the higher tiers take more of it.
            var flax = GetOrCreateItem(items.Entities, "Flax", "A tall blue-flowered plant. Its fibers are woven into linen.", ItemCategory.Resource, ItemType.Farming);
            var linen = GetOrCreateItem(items.Entities, "Linen", "Cloth woven from flax, for robes and vestments.", ItemCategory.Resource, ItemType.Crafting);
            var hide = GetOrCreateItem(items.Entities, "Hide", "The raw skin of a beast, taken while fighting. Tanned, it becomes leather.", ItemCategory.Resource, ItemType.Crafting);
            var leather = GetOrCreateItem(items.Entities, "Leather", "Tanned hide, for jerkins, straps and the padding under mail and plate.", ItemCategory.Resource, ItemType.Crafting);
            // a drop of level 1 would be worth 1 coin (flax, a level 5 crop, is worth 5); the cloths are worth their two
            // pieces, said here so the first start prices the gear from them whatever order the items come in
            if (hide.ShopSellPrice <= 1) hide.ShopSellPrice = 5;
            if (linen.ShopSellPrice <= 1) linen.ShopSellPrice = 10;
            if (leather.ShopSellPrice <= 1) leather.ShopSellPrice = 10;

            EnsureDropRate(5, flax, 20, 0.15, farming);
            // FightingTaskProcessor rolls the Slayer drops while a combat skill is trained
            EnsureDropRate(1, hide, 20, 0.2, slayer);
            EnsureExactCraftingRecipe(1, linen, new Ingredient { Item = flax, Amount = 2 });
            EnsureExactCraftingRecipe(1, leather, new Ingredient { Item = hide, Amount = 2 });

            // the metal of each tier's level, for trim and buckles or for the piece itself
            var bars = new[] { known.BronzeBar, known.MithrilBar, known.RuneBar, known.AbraxasBar, known.LioniteBar, known.AtlarusBar, known.ElderMithrilBar, known.ElderEthereumBar };
            var plate = new[]
            {
                known.Bronze, known.Iron, known.Steel, known.Black, known.Mithril, known.Adamantite, known.Rune, known.Dragon, known.Abraxas,
                known.Phantom, known.Lionsbane, known.Ether, known.Ancient, known.Atlarus,
                known.ElderBronze, known.ElderIron, known.ElderSteel, known.ElderBlack, known.ElderMithril, known.ElderAdamantite, known.ElderRune,
                known.ElderDragon, known.ElderAbraxas, known.ElderPhantom, known.ElderLionsbane, known.ElderEther, known.ElderAncient, known.ElderAtlarus,
            };

            var byName = new Dictionary<string, Item>(StringComparer.OrdinalIgnoreCase);
            foreach (var existing in items.Entities)
            {
                if (!string.IsNullOrEmpty(existing.Name)) byName[existing.Name] = existing;
            }

            var created = 0;
            var corrected = 0;
            foreach (var piece in ClassGearPieces)
            {
                var info = ClassGearLines.First(x => x.Line == piece.Line);
                var factor = ItemLines.ArmorFactor(piece.Line);
                for (var tier = 1; tier <= ItemLines.Tiers; ++tier)
                {
                    var material = ItemLines.Material(piece.Line, tier);
                    var level = ItemLines.TierLevels[tier - 1];
                    var id = ClassGearItemId(piece.Line, tier, piece.Key);
                    var name = material + " " + piece.Name;

                    if (byName.TryGetValue(name, out var sameName) && sameName.Id != id)
                    {
                        logger.LogError("Class gear: '{Name}' is already the name of another item ({Id}); the piece was not made.", name, sameName.Id);
                        continue;
                    }

                    // a shade above so 0.4 x 20 is 8 and not 9; rounded up, so a piece never has less than its share of plate
                    var armor = (int)Math.Ceiling(PlateArmorAt(plate, ClassGearPlateSlot(piece.Type), level) * factor - 0.000001);
                    var stats = ClassGearStats(piece.Line, tier, piece.Type);

                    var item = GetItem(id);
                    var isNew = item == null;
                    if (isNew)
                    {
                        item = new Item
                        {
                            Id = id,
                            Name = name,
                            RequiredCraftingLevel = 1000, // the old columns, as on every item made here: the recipe is the record
                            RequiredCookingLevel = 1000,
                            Craftable = false,
                        };
                    }

                    var changed = false;
                    void Fix<T>(T current, T wanted, Action<T> set)
                    {
                        if (EqualityComparer<T>.Default.Equals(current, wanted)) return;
                        set(wanted);
                        changed = true;
                    }

                    Fix(item.Name, name, v => { byName.Remove(item.Name ?? ""); item.Name = v; });
                    Fix(item.Description, info.Description, v => item.Description = v);
                    Fix(item.Category, (int)ItemCategory.Armor, v => item.Category = v);
                    Fix(item.Type, (int)piece.Type, v => item.Type = v);
                    Fix(item.Material, (int)material, v => item.Material = v);
                    Fix(item.Level, level, v => item.Level = v);
                    Fix(item.MaleModelId, piece.MaleModels, v => item.MaleModelId = v);
                    Fix(item.FemaleModelId, piece.FemaleModels, v => item.FemaleModelId = v);
                    Fix(item.HeadMask, (int)piece.Mask, v => item.HeadMask = v);
                    // the parts and the tier's material are the look: no prefabs
                    Fix(item.GenericPrefab, null, v => item.GenericPrefab = v);
                    Fix(item.MalePrefab, null, v => item.MalePrefab = v);
                    Fix(item.FemalePrefab, null, v => item.FemalePrefab = v);
                    Fix(item.IsGenericModel, false, v => item.IsGenericModel = v);
                    Fix(item.ArmorPower, armor, v => item.ArmorPower = v);
                    Fix(item.WeaponAim, 0, v => item.WeaponAim = v);
                    Fix(item.WeaponPower, 0, v => item.WeaponPower = v);
                    Fix(item.RangedAim, 0, v => item.RangedAim = v);
                    Fix(item.RangedPower, 0, v => item.RangedPower = v);
                    Fix(item.MagicAim, 0, v => item.MagicAim = v);
                    Fix(item.MagicPower, 0, v => item.MagicPower = v);
                    Fix(item.ExtraStats, stats, v => item.ExtraStats = v);
                    Fix(item.RequiredMagicLevel, info.Requires == ClassGearSkill.Magic ? level : 0, v => item.RequiredMagicLevel = v);
                    Fix(item.RequiredRangedLevel, info.Requires == ClassGearSkill.Ranged ? level : 0, v => item.RequiredRangedLevel = v);
                    Fix(item.RequiredAttackLevel, info.Requires == ClassGearSkill.Attack ? level : 0, v => item.RequiredAttackLevel = v);
                    Fix(item.RequiredDefenseLevel, info.Requires == ClassGearSkill.Defense ? level : 0, v => item.RequiredDefenseLevel = v);
                    Fix(item.RequiredSlayerLevel, 0, v => item.RequiredSlayerLevel = v);
                    Fix(item.ShopBuyPrice, 0L, v => item.ShopBuyPrice = v); // made, not bought; the sell price follows the recipe (ApplyVendorPrices)
                    Fix(item.Soulbound, false, v => item.Soulbound = v);
                    Fix(item.Hidden, false, v => item.Hidden = v);

                    if (isNew)
                    {
                        item.Modified = DateTime.UtcNow;
                        Add(item);
                        created++;
                    }
                    else if (changed)
                    {
                        item.Modified = DateTime.UtcNow;
                        corrected++;
                    }
                    byName[name] = item;

                    // the recipe: Crafting at the tier's level, always a success, as the metal tiers
                    var bar = bars[tier - 1];
                    Ingredient[] ingredients;
                    switch (piece.Line)
                    {
                        case ItemLine.Mage:
                        case ItemLine.Healer:
                            ingredients = new[] { new Ingredient { Item = linen, Amount = ClassGearAmount(piece.Type, ClassGearChestCloth, ClassGearLeggingsCloth, ClassGearOtherCloth) * tier }, new Ingredient { Item = bar, Amount = 1 } };
                            break;
                        case ItemLine.Ranger:
                            ingredients = new[] { new Ingredient { Item = leather, Amount = ClassGearAmount(piece.Type, ClassGearChestCloth, ClassGearLeggingsCloth, ClassGearOtherCloth) * tier }, new Ingredient { Item = bar, Amount = 1 } };
                            break;
                        case ItemLine.Melee:
                            ingredients = new[] { new Ingredient { Item = bar, Amount = ClassGearAmount(piece.Type, ClassGearChestBars, ClassGearLeggingsBars, ClassGearOtherBars) }, new Ingredient { Item = leather, Amount = tier } };
                            break;
                        default:
                            ingredients = new[] { new Ingredient { Item = bar, Amount = ClassGearAmount(piece.Type, ClassGearTankChestBars, ClassGearTankLeggingsBars, ClassGearTankOtherBars) }, new Ingredient { Item = leather, Amount = tier } };
                            break;
                    }

                    var recipe = EnsureExactCraftingRecipe(level, item, ingredients);
                    if (recipe.Name != item.Name) recipe.Name = item.Name;
                    if (recipe.Description != item.Description) recipe.Description = item.Description;
                }
            }

            if (created > 0 || corrected > 0)
                logger.LogInformation("Class gear: {Created} item(s) created, {Corrected} corrected.", created, corrected);
        }

        private static int ClassGearAmount(ItemType type, int chest, int leggings, int other)
        {
            return type == ItemType.Chest ? chest : type == ItemType.Leggings ? leggings : other;
        }

        /// <summary>
        ///     A crafting recipe with exactly these ingredients: <see cref="EnsureRecipe"/> only looks at how many
        ///     there are and what they add up to, so a changed ingredient of the same amount would stay.
        /// </summary>
        private ItemRecipe EnsureExactCraftingRecipe(int level, Item target, params Ingredient[] wanted)
        {
            var recipe = GetItemRecipeByItem(target.Id);
            if (recipe != null)
            {
                var have = GetRecipeIngredients(recipe.Id);
                var same = have.Count == wanted.Length && wanted.All(w => have.Any(h => h.ItemId == w.Item.Id && h.Amount == w.Amount));
                if (!same)
                {
                    foreach (var old in have.ToList()) Remove(old);
                }
            }
            return EnsureCraftingRecipe(level, target, wanted);
        }
    }
}
