using System;
using Microsoft.Extensions.Logging;
using RavenNest.Combat;
using RavenNest.DataModels;

namespace RavenNest.BusinessLogic.Data
{
    public partial class GameData
    {
        private static readonly (GemKind Kind, int Level, string Description)[] RawGems =
        {
            (GemKind.Topaz, 40, null),
            (GemKind.Amethyst, 60, "A violet gemstone that hums faintly when held. Enchanted, it eases healing and every hurt a little."),
            (GemKind.Onyx, 80, "A black gemstone, cold and heavy. Enchanted, it binds life to its bearer."),
            (GemKind.Diamond, 100, "The hardest stone there is, and the brightest. Enchanted, it finds the weak spot."),
        };

        /// <summary>
        ///     Gems (the game's docs/gems-design.md): the three new raw gems, a mining drop for Topaz (it had none) and
        ///     the new ones, and the 35 enchanted gems with the fixed ids RavenNest.Combat.Gems gives them, so the game
        ///     and the website know a gem from its id alone. Prices follow the raw gem, three times per tier, so
        ///     combining never pays at the vendor.
        /// </summary>
        /// <summary>Rookery, the clan skill that goes with Taming (RavenNest.Combat.Rookery), made once like Enchanting's row.</summary>
        private void EnsureRookery()
        {
            var all = System.Linq.Enumerable.ToList(skills.Entities);
            // a fixed id: the clans' Rookery rows point at it, so it must be the same after every start. The skill set is
            // not saved, so this row is made in memory at each start (the id spells its first name, Menagerie; a row
            // ever saved under that name would be renamed rather than made again)
            var id = new Guid("6d656e61-6765-7269-6500-000000000001");
            var made = all.Find(x => x.Id == id);
            if (made != null)
            {
                if (made.Name != RavenNest.Combat.Rookery.Name) made.Name = RavenNest.Combat.Rookery.Name;
                return;
            }
            if (all.Exists(x => x.Name == RavenNest.Combat.Rookery.Name)) return;
            var enchanting = all.Find(x => x.Name == "Enchanting");
            skills.Add(new Skill
            {
                Id = id,
                Name = RavenNest.Combat.Rookery.Name,
                MaxLevel = enchanting?.MaxLevel ?? GameMath.MaxLevel,
                RequiredClanLevel = RavenNest.Combat.Rookery.RequiredClanLevel,
                Type = enchanting?.Type ?? 0,
            });
        }

        private void EnsureGems()
        {
            var mining = RavenNest.Models.Skill.Mining;
            foreach (var (kind, level, description) in RawGems)
            {
                var raw = GetOrCreateItem(items.Entities, Gems.RawName(kind), description, ItemCategory.Resource, ItemType.Mining);
                if (raw.ShopSellPrice == 0) raw.ShopSellPrice = 50 + level * 5;
                EnsureDropRate(level, raw, 120, 0.2, mining);
            }

            var created = 0;
            foreach (GemKind kind in Enum.GetValues(typeof(GemKind)))
            {
                var raw = GetOrCreateItem(items.Entities, Gems.RawName(kind), ItemCategory.Resource, ItemType.Mining);
                var basePrice = Math.Max(10, raw.ShopSellPrice);
                foreach (GemTier tier in Enum.GetValues(typeof(GemTier)))
                {
                    var id = Gems.ItemId(kind, tier);
                    var name = Gems.Name(kind, tier);
                    var description = "An enchanted " + kind.ToString().ToLowerInvariant() + ", ready for a socket. In a weapon: "
                        + Gems.Describe(kind, tier, GemSlot.Weapon) + ". In armour: " + Gems.Describe(kind, tier, GemSlot.Armor)
                        + ". In a ring or amulet: " + Gems.Describe(kind, tier, GemSlot.Jewellery) + ".";
                    var price = basePrice * (long)Math.Pow(3, (int)tier);
                    var item = GetItem(id);
                    if (item == null)
                    {
                        item = new Item
                        {
                            Id = id,
                            Name = name,
                            Description = description,
                            Category = (int)ItemCategory.Resource,
                            Type = (int)ItemType.None,
                            RequiredCraftingLevel = 1000,
                            RequiredCookingLevel = 1000,
                            Craftable = false,
                            Level = 1,
                            ShopSellPrice = price,
                            Modified = DateTime.UtcNow,
                        };
                        Add(item);
                        created++;
                        continue;
                    }

                    if (item.Name != name) item.Name = name;
                    if (item.Description != description) item.Description = description;
                    if (item.ShopSellPrice != price) item.ShopSellPrice = price;
                }
            }

            if (created > 0) logger.LogInformation("Gems: {Count} enchanted gem item(s) created.", created);
        }

        /// <summary>
        ///     The pet eggs (RavenNest.Combat.PetEggs): one item per hatched species with the fixed id the library gives it,
        ///     so the game and the website know an egg from its id. Tradeable resources; `!pets hatch` uses them.
        /// </summary>
        private void EnsurePetEggs()
        {
            var created = 0;
            foreach (var species in PetEggs.Species)
            {
                var id = PetEggs.ItemId(species.Id);
                var name = species.EggName;
                var description = "Warm to the touch. Hatches into a " + species.NameAt(0) + " (" + species.Role.ToString().ToLowerInvariant()
                    + ") after " + (int)species.HatchMinutes + " minutes of play: !pets hatch " + species.NameAt(0).ToLowerInvariant()
                    + ". Needs Taming " + species.TameLevel + ".";
                var price = 500 + species.TameLevel * 100;
                var item = GetItem(id);
                if (item == null)
                {
                    Add(new Item
                    {
                        Id = id,
                        Name = name,
                        Description = description,
                        Category = (int)ItemCategory.Resource,
                        Type = (int)ItemType.None,
                        RequiredCraftingLevel = 1000,
                        RequiredCookingLevel = 1000,
                        Craftable = false,
                        Level = 1,
                        ShopSellPrice = price,
                        Modified = DateTime.UtcNow,
                    });
                    created++;
                    continue;
                }
                if (item.Name != name) item.Name = name;
                if (item.Description != description) item.Description = description;
                if (item.ShopSellPrice != price) item.ShopSellPrice = price;
            }
            if (created > 0) logger.LogInformation("Pet eggs: {Count} egg item(s) created.", created);
        }
    }
}
