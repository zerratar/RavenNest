using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using RavenNest.Combat;
using RavenNest.DataModels;

namespace RavenNest.BusinessLogic.Data
{
    /// <summary>
    ///     The Legendary tier's items (the game's docs/legendary-design.md): five Dark Fortress uniques whose numbers are
    ///     never set by hand. Each is the best non-Legendary item of its type plus a margin (RavenNest.Combat.Legendary),
    ///     worked out from the catalog at start and again when an item is added or changed, so a better weapon added later
    ///     keeps them on top. They ask for 950 in their skill, are soulbound, sell for a coin and drop from no chest.
    /// </summary>
    public partial class GameData
    {
        private readonly object legendaryMutex = new object();

        /// <summary>The best accuracy and power (armor for a shield) of the normal items of a Legendary's type.</summary>
        public (int Accuracy, int Power, int Armor) BestNormal(LegendaryInfo legendary)
        {
            int accuracy = 0, power = 0, armor = 0;
            foreach (var item in items.Entities)
            {
                if (item == null || item.Type != legendary.ItemType || Legendary.IsLegendary(item.Id) || item.Hidden) continue;
                switch (legendary.Skill)
                {
                    case LegendarySkill.Attack:
                        accuracy = Math.Max(accuracy, item.WeaponAim);
                        power = Math.Max(power, item.WeaponPower);
                        break;
                    case LegendarySkill.Magic:
                        accuracy = Math.Max(accuracy, item.MagicAim);
                        power = Math.Max(power, item.MagicPower);
                        break;
                    case LegendarySkill.Ranged:
                        accuracy = Math.Max(accuracy, item.RangedAim);
                        power = Math.Max(power, item.RangedPower);
                        break;
                }
                armor = Math.Max(armor, item.ArmorPower);
            }
            return (accuracy, power, armor);
        }

        /// <summary>
        ///     Puts each Legendary's item row right: rank 1 numbers from the catalog, the 950 requirement, soulbound, a
        ///     coin's price, no chest drop. Safe to call again (when the catalog changes); only a row that differs changes.
        /// </summary>
        public void EnsureLegendaries()
        {
            lock (legendaryMutex)
            {
                var corrected = 0;
                foreach (var legendary in Legendary.All)
                {
                    var item = GetItem(legendary.ItemId);
                    if (item == null)
                    {
                        logger.LogError("Legendary: the item for {Name} ({Id}) is missing.", legendary.Name, legendary.ItemId);
                        continue;
                    }

                    var best = BestNormal(legendary);
                    var changed = false;
                    void Fix<T>(T current, T wanted, Action<T> set)
                    {
                        if (EqualityComparer<T>.Default.Equals(current, wanted)) return;
                        set(wanted);
                        changed = true;
                    }

                    var accuracy = Legendary.Derive(best.Accuracy);
                    var power = Legendary.Derive(best.Power);
                    var melee = legendary.Skill == LegendarySkill.Attack;
                    var magic = legendary.Skill == LegendarySkill.Magic;
                    var ranged = legendary.Skill == LegendarySkill.Ranged;
                    Fix(item.WeaponAim, melee ? accuracy : 0, v => item.WeaponAim = v);
                    Fix(item.WeaponPower, melee ? power : 0, v => item.WeaponPower = v);
                    Fix(item.MagicAim, magic ? accuracy : 0, v => item.MagicAim = v);
                    Fix(item.MagicPower, magic ? power : 0, v => item.MagicPower = v);
                    Fix(item.RangedAim, ranged ? accuracy : 0, v => item.RangedAim = v);
                    Fix(item.RangedPower, ranged ? power : 0, v => item.RangedPower = v);
                    Fix(item.ArmorPower, legendary.IsShield ? Legendary.Derive(best.Armor) : 0, v => item.ArmorPower = v);

                    var level = Legendary.RequiredLevel;
                    Fix(item.RequiredAttackLevel, legendary.Skill == LegendarySkill.Attack ? level : 0, v => item.RequiredAttackLevel = v);
                    Fix(item.RequiredMagicLevel, legendary.Skill == LegendarySkill.Magic ? level : 0, v => item.RequiredMagicLevel = v);
                    Fix(item.RequiredRangedLevel, legendary.Skill == LegendarySkill.Ranged ? level : 0, v => item.RequiredRangedLevel = v);
                    Fix(item.RequiredDefenseLevel, legendary.Skill == LegendarySkill.Defense ? level : 0, v => item.RequiredDefenseLevel = v);
                    Fix(item.RequiredSlayerLevel, 0, v => item.RequiredSlayerLevel = v);
                    Fix(item.Level, level, v => item.Level = v);
                    Fix(item.Soulbound, true, v => item.Soulbound = v);
                    Fix(item.ShopSellPrice, Legendary.SellPrice, v => item.ShopSellPrice = v);
                    Fix(item.ShopBuyPrice, 0L, v => item.ShopBuyPrice = v);
                    Fix(item.Craftable, false, v => item.Craftable = v);

                    if (changed)
                    {
                        item.Modified = DateTime.UtcNow;
                        corrected++;
                    }

                    // never from a chest again
                    foreach (var drop in itemDrops[nameof(Item), item.Id].ToList())
                    {
                        Remove(drop);
                        corrected++;
                    }
                }

                if (corrected > 0)
                    logger.LogInformation("Legendaries: {Count} change(s) to the five items and their drops.", corrected);
            }
        }
    }
}
