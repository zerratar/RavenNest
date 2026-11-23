using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using RavenNest.DataModels;

namespace RavenNest.BusinessLogic.Data
{
    /// <summary>
    ///     The Dark Fortress gear (v0.9.7, the game's docs/dark-fortress-design.md): weapons, shields and head pieces made
    ///     from Synty's POLYGON Dark Fortress pack. Six crafted "Dreadforged" pieces fill the gap between Atlarus (400)
    ///     and Elder Bronze (500) on the weapon ladders; the rest are uniques: trophies of the tower's floor 9 boss (the
    ///     Behemoth, <see cref="RavenNest.Combat.TowerBoss.Trophies"/>) and rare drops of the higher dungeon and tower
    ///     chests. The head pieces of the class gear lines are in GameData.ClassGear (head3).
    ///     Like the class gear, the code is the record: a row that differs is put right at the next start, and the ids
    ///     are made from a fixed key, so they are the same in every database.
    /// </summary>
    public partial class GameData
    {
        private sealed class DarkFortressPiece
        {
            /// <summary>Stays when the name changes: the item's id is made from it.</summary>
            public string Key;
            public string Name;
            public string Description;
            public ItemCategory Category;
            public ItemType Type;
            /// <summary>The model, under Assets/Resources in the game.</summary>
            public string Prefab;
            /// <summary>The skill level it asks for (Attack, Magic, Ranged or Defense by its type), 0 for none.</summary>
            public int Level;
            public int Accuracy;
            public int Power;
            public int Armor;
            /// <summary>What the classic columns cannot say, in RavenNest.Combat.GearStats' text form.</summary>
            public string Extra;
            /// <summary>Crafted from these (with the Crafting level), or null for a unique.</summary>
            public Func<TypedItems, Ingredient[]> Recipe;
            public int CraftingLevel;
            /// <summary>A unique's place in the dungeon and tower chests' drop table: the tier (0 to 4), its weight and the Slayer level; tier -1 for none.</summary>
            public int DropTier = -1;
            public double DropWeight;
            public int DropSlayer;
            public long SellPrice;
        }

        private const string DarkFortressWeapons = "Character/Weapons/DarkFortress/DF ";

        private static readonly DarkFortressPiece[] DarkFortressPieces =
        {
            // ---- Dreadforged: crafted at 450, halfway between Atlarus (400) and Elder Bronze (500), with a little of the
            //      fortress in each. Atlarus bars with one Elder Iron bar (smelted from 450) of temper.
            new DarkFortressPiece { Key = "dreadforged-sword", Name = "Dreadforged Sword", Type = ItemType.OneHandedSword, Prefab = DarkFortressWeapons + "Sword 03",
                Description = "Forged in the dark fortress from Atlarus and elder iron. It drinks a little of every wound it makes.",
                Level = 450, Accuracy = 275, Power = 275, Extra = "LIFE_STEAL:2%", CraftingLevel = 455, Recipe = k => Bars(k, 3, 1) },
            new DarkFortressPiece { Key = "dreadforged-morningstar", Name = "Dreadforged Morningstar", Type = ItemType.OneHandedAxe, Prefab = DarkFortressWeapons + "Mace 02",
                Description = "A spiked ball of Atlarus on an elder iron haft. Armor is more of a suggestion to it.",
                Level = 450, Accuracy = 258, Power = 291, Extra = "ARMOR_PENETRATION:6%", CraftingLevel = 459, Recipe = k => Bars(k, 4, 1) },
            new DarkFortressPiece { Key = "dreadforged-cleaver", Name = "Dreadforged Cleaver", Type = ItemType.TwoHandedSword, Prefab = DarkFortressWeapons + "Sword 02",
                Description = "A great slab of a blade, cracked and still sharp. It drinks a little of every wound it makes.",
                Level = 450, Accuracy = 390, Power = 390, Extra = "LIFE_STEAL:2%", CraftingLevel = 463, Recipe = k => Bars(k, 5, 2) },
            new DarkFortressPiece { Key = "dreadforged-battleaxe", Name = "Dreadforged Battleaxe", Type = ItemType.TwoHandedAxe, Prefab = DarkFortressWeapons + "Axe 01",
                Description = "Two crescent blades on a long haft, made to open armor like a can.",
                Level = 450, Accuracy = 343, Power = 429, Extra = "ARMOR_PENETRATION:6%", CraftingLevel = 463, Recipe = k => Bars(k, 5, 2) },
            new DarkFortressPiece { Key = "dreadforged-staff", Name = "Dreadforged Staff", Type = ItemType.TwoHandedStaff, Prefab = DarkFortressWeapons + "Staff 02",
                Description = "Shadowoak grown around a spine of Atlarus. Its spells take a little life back with them.",
                Level = 450, Accuracy = 390, Power = 390, Extra = "LIFE_STEAL:2%", CraftingLevel = 463, Recipe = k => Bars(k, 4, 1, k.ShadowoakLogs, 2) },
            new DarkFortressPiece { Key = "dreadforged-shield", Name = "Dreadforged Shield", Category = ItemCategory.Armor, Type = ItemType.Shield, Prefab = DarkFortressWeapons + "Shield 02",
                Description = "A kite shield of the fortress guard. Whoever strikes it feels it too.",
                Level = 450, Armor = 226, Extra = "THORNS:4%", CraftingLevel = 464, Recipe = k => Bars(k, 4, 1) },

            // ---- the Gardener's trophies (the tower's floor 9 boss, the Behemoth): at the floor's level
            new DarkFortressPiece { Key = "gardeners-bonesaw", Name = "Gardeners Bonesaw", Type = ItemType.TwoHandedAxe, Prefab = DarkFortressWeapons + "Saw 01",
                Description = "The Gardener pruned the Deep Roots with it. Mostly the climbers.",
                Level = 90, Accuracy = 75, Power = 94, Extra = "POISON_DAMAGE:25%", SellPrice = 4000 },
            new DarkFortressPiece { Key = "rotreaper", Name = "Rotreaper", Type = ItemType.TwoHandedStaff, Prefab = DarkFortressWeapons + "Scythe 01",
                Description = "A scythe for a harvest nobody wanted. Its spells leave a green rot behind.",
                Level = 90, Accuracy = 86, Power = 86, Extra = "POISON_DAMAGE:25%", SellPrice = 4000 },
            new DarkFortressPiece { Key = "plague-mitre", Name = "Plague Mitre", Category = ItemCategory.Cosmetic, Type = ItemType.Hat, Prefab = DarkFortressHead + "Crown 02",
                Description = "The Gardener's Sunday hat. It still smells of compost.", SellPrice = 2500 },

            // ---- rare finds in the dungeon and tower chests (epic tier, by Slayer level)
            new DarkFortressPiece { Key = "skullcrusher", Name = "Skullcrusher", Type = ItemType.OneHandedAxe, Prefab = DarkFortressWeapons + "Club 01",
                Description = "A club with a skull on the end. Whose skull? Best not to ask.",
                Level = 300, Accuracy = 150, Power = 170, Extra = "CRIT_DAMAGE:15%", DropTier = 3, DropWeight = 0.01, DropSlayer = 30, SellPrice = 12000 },
            new DarkFortressPiece { Key = "fortress-hand-cannon", Name = "Fortress Hand Cannon", Type = ItemType.TwoHandedBow, Prefab = DarkFortressWeapons + "Cannon 01",
                Description = "Very loud, very heavy, and somehow it still shoots arrows. Nobody in the fortress knows how either.",
                Level = 500, Accuracy = 470, Power = 480, Extra = "CRIT_CHANCE:5%", DropTier = 4, DropWeight = 0.01, DropSlayer = 50, SellPrice = 20000 },
            new DarkFortressPiece { Key = "gravebloom-warhammer", Name = "Gravebloom Warhammer", Type = ItemType.OneHandedAxe, Prefab = DarkFortressWeapons + "Hammer 01",
                Description = "Its green stone grew in a grave. What it hits feels sick for days.",
                Level = 650, Accuracy = 357, Power = 404, Extra = "POISON_DAMAGE:35%", DropTier = 4, DropWeight = 0.01, DropSlayer = 65, SellPrice = 27000 },
            new DarkFortressPiece { Key = "sunstaff-of-the-inquisitor", Name = "Sunstaff of the Inquisitor", Type = ItemType.TwoHandedStaff, Prefab = DarkFortressWeapons + "Staff 03",
                Description = "The Inquisitor's golden staff. Its light mends friends and burns the restless dead.",
                Level = 700, Accuracy = 585, Power = 585, Extra = "HEALING_POWER_BONUS:10%;UNDEAD_DAMAGE:15%", DropTier = 4, DropWeight = 0.01, DropSlayer = 70, SellPrice = 32000 },
            new DarkFortressPiece { Key = "hellhorn-mace", Name = "Hellhorn Mace", Type = ItemType.OneHandedAxe, Prefab = DarkFortressWeapons + "Mace 01",
                Description = "Horned, red hot at the tips, and never quite cool to hold.",
                Level = 750, Accuracy = 386, Power = 436, Extra = "FIRE_DAMAGE:35%", DropTier = 4, DropWeight = 0.01, DropSlayer = 75, SellPrice = 41000 },
            new DarkFortressPiece { Key = "batwing-staff", Name = "Batwing Staff", Type = ItemType.TwoHandedStaff, Prefab = DarkFortressWeapons + "Staff 01",
                Description = "The Necromancer's staff. The skull on top grins wider every time it takes a life.",
                Level = 800, Accuracy = 608, Power = 608, Extra = "LIFE_STEAL:3%", DropTier = 4, DropWeight = 0.01, DropSlayer = 80, SellPrice = 51000 },
            new DarkFortressPiece { Key = "spiked-bulwark", Name = "Spiked Bulwark", Category = ItemCategory.Armor, Type = ItemType.Shield, Prefab = DarkFortressWeapons + "Shield 01",
                Description = "A black shield bristling with spikes. Hitting it is a mistake you make once.",
                Level = 850, Armor = 380, Extra = "THORNS:8%", DropTier = 4, DropWeight = 0.01, DropSlayer = 85, SellPrice = 40000 },
            new DarkFortressPiece { Key = "soulreaver", Name = "Soulreaver", Type = ItemType.TwoHandedSword, Prefab = DarkFortressWeapons + "Sword 01",
                Description = "The fortress lord's greatsword. Its pale blade glows brighter with every soul it takes.",
                Level = 900, Accuracy = 711, Power = 711, Extra = "LIFE_STEAL:3%", DropTier = 4, DropWeight = 0.01, DropSlayer = 90, SellPrice = 94000 },
            new DarkFortressPiece { Key = "thorn-crown", Name = "Thorn Crown", Category = ItemCategory.Cosmetic, Type = ItemType.Hat, Prefab = DarkFortressHead + "Crown 01",
                Description = "A crown of black iron spikes. Uncomfortable, but very regal.", DropTier = 3, DropWeight = 0.01, SellPrice = 2500 },
            new DarkFortressPiece { Key = "grinning-skull", Name = "Grinning Skull", Category = ItemCategory.Cosmetic, Type = ItemType.Mask, Prefab = DarkFortressHead + "Mask 04",
                Description = "A skull worn as a mask, its eyes still glowing green. It is always smiling at you.", DropTier = 3, DropWeight = 0.01, SellPrice = 2500 },
        };

        /// <summary>Atlarus bars, Elder Iron bars and anything else a Dreadforged piece is made of.</summary>
        private static Ingredient[] Bars(TypedItems k, int atlarus, int elderIron, Item other = null, int otherAmount = 0)
        {
            var list = new List<Ingredient> { new Ingredient { Item = k.AtlarusBar, Amount = atlarus }, new Ingredient { Item = k.ElderIronBar, Amount = elderIron } };
            if (other != null && otherAmount > 0) list.Add(new Ingredient { Item = other, Amount = otherAmount });
            return list.ToArray();
        }

        /// <summary>The item id of a Dark Fortress piece: fixed, the same in every database.</summary>
        public static Guid DarkFortressItemId(string key)
        {
            byte[] hash;
            using (var md5 = MD5.Create()) hash = md5.ComputeHash(Encoding.UTF8.GetBytes("ravenfall-dark-fortress-" + key));
            hash[6] = (byte)((hash[6] & 0x0F) | 0x30);
            hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
            return new Guid(hash);
        }

        /// <summary>The id of a Dark Fortress piece by its name (a tower boss's trophies are named), or null.</summary>
        public static Guid? DarkFortressItemByName(string name)
        {
            var piece = DarkFortressPieces.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            return piece != null ? DarkFortressItemId(piece.Key) : (Guid?)null;
        }

        private void EnsureDarkFortress()
        {
            var known = GetKnownItems();
            var byName = new Dictionary<string, Item>(StringComparer.OrdinalIgnoreCase);
            foreach (var existing in items.Entities)
            {
                if (!string.IsNullOrEmpty(existing.Name)) byName[existing.Name] = existing;
            }

            var created = 0;
            var corrected = 0;
            foreach (var piece in DarkFortressPieces)
            {
                var id = DarkFortressItemId(piece.Key);
                if (byName.TryGetValue(piece.Name, out var sameName) && sameName.Id != id)
                {
                    logger.LogError("Dark Fortress: '{Name}' is already the name of another item ({Id}); the piece was not made.", piece.Name, sameName.Id);
                    continue;
                }

                var category = piece.Category == ItemCategory.Weapon && piece.Type == ItemType.Shield ? ItemCategory.Armor : piece.Category;
                var item = GetItem(id);
                var isNew = item == null;
                if (isNew)
                {
                    item = new Item
                    {
                        Id = id,
                        Name = piece.Name,
                        RequiredCraftingLevel = 1000, // the old columns: the recipe is the record
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

                var magic = piece.Type == ItemType.TwoHandedStaff;
                var ranged = piece.Type == ItemType.TwoHandedBow;
                var armor = category == ItemCategory.Armor;
                var melee = category == ItemCategory.Weapon && !magic && !ranged;

                Fix(item.Name, piece.Name, v => item.Name = v);
                Fix(item.Description, piece.Description, v => item.Description = v);
                Fix(item.Category, (int)category, v => item.Category = v);
                Fix(item.Type, (int)piece.Type, v => item.Type = v);
                Fix(item.Material, (int)ItemMaterial.None, v => item.Material = v);
                // a crafted piece's level is its recipe's (the recipe sets it; fixing it here too would flip it every start)
                Fix(item.Level, Math.Max(1, piece.Recipe != null ? piece.CraftingLevel : piece.Level), v => item.Level = v);
                Fix(item.MaleModelId, null, v => item.MaleModelId = v);
                Fix(item.FemaleModelId, null, v => item.FemaleModelId = v);
                Fix(item.HeadMask, 0, v => item.HeadMask = v);
                Fix(item.GenericPrefab, piece.Prefab, v => item.GenericPrefab = v);
                Fix(item.MalePrefab, null, v => item.MalePrefab = v);
                Fix(item.FemalePrefab, null, v => item.FemalePrefab = v);
                Fix(item.IsGenericModel, true, v => item.IsGenericModel = v);
                Fix(item.ArmorPower, piece.Armor, v => item.ArmorPower = v);
                Fix(item.WeaponAim, melee ? piece.Accuracy : 0, v => item.WeaponAim = v);
                Fix(item.WeaponPower, melee ? piece.Power : 0, v => item.WeaponPower = v);
                Fix(item.MagicAim, magic ? piece.Accuracy : 0, v => item.MagicAim = v);
                Fix(item.MagicPower, magic ? piece.Power : 0, v => item.MagicPower = v);
                Fix(item.RangedAim, ranged ? piece.Accuracy : 0, v => item.RangedAim = v);
                Fix(item.RangedPower, ranged ? piece.Power : 0, v => item.RangedPower = v);
                Fix(item.ExtraStats, piece.Extra, v => item.ExtraStats = v);
                Fix(item.RequiredAttackLevel, melee ? piece.Level : 0, v => item.RequiredAttackLevel = v);
                Fix(item.RequiredMagicLevel, magic ? piece.Level : 0, v => item.RequiredMagicLevel = v);
                Fix(item.RequiredRangedLevel, ranged ? piece.Level : 0, v => item.RequiredRangedLevel = v);
                Fix(item.RequiredDefenseLevel, armor ? piece.Level : 0, v => item.RequiredDefenseLevel = v);
                Fix(item.RequiredSlayerLevel, 0, v => item.RequiredSlayerLevel = v);
                Fix(item.ShopBuyPrice, 0L, v => item.ShopBuyPrice = v);
                if (piece.Recipe == null && piece.SellPrice > 0) Fix(item.ShopSellPrice, piece.SellPrice, v => item.ShopSellPrice = v);
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
                byName[piece.Name] = item;

                if (piece.Recipe != null)
                {
                    var recipe = EnsureExactCraftingRecipe(piece.CraftingLevel, item, piece.Recipe(known));
                    if (recipe.Name != item.Name) recipe.Name = item.Name;
                    if (recipe.Description != item.Description) recipe.Description = item.Description;
                }

                if (piece.DropTier >= 0)
                {
                    EnsureDrop(item, piece.DropWeight, piece.DropWeight, piece.DropTier, piece.DropSlayer);
                }
            }

            if (created > 0 || corrected > 0)
                logger.LogInformation("Dark Fortress gear: {Created} item(s) created, {Corrected} corrected.", created, corrected);
        }
    }
}
