using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Extended;
using RavenNest.BusinessLogic.Extensions;
using RavenNest.BusinessLogic.Providers;
using RavenNest.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RavenNest.BusinessLogic.Game.Enchantment
{
    public class SkillBonus
    {
        public ItemEnchantment Enchantment { get; set; }
        public PlayerSkill Skill { get; set; }
        public double Bonus { get; set; }
    }


    public static class EnchantmentExtensions
    {
        public static IReadOnlyList<SkillBonus> GetSkillBonuses(this ReadOnlyInventoryItem item, IReadOnlyList<PlayerSkill> playerSkills, GameData gameData)
        {
            var enchantments = GetItemEnchantments(item, gameData.GetItemAttributes());
            var result = new List<SkillBonus>();

            if (enchantments != null)
            {
                foreach (var e in enchantments)
                {
                    var value = e.Value;
                    var key = e.Name.ToUpper();
                    PlayerSkill targetSkill = null;
                    foreach (var skill in playerSkills)
                    {
                        if (skill.Name.ToUpper() == key)
                        {
                            targetSkill = skill;
                            break;
                        }
                    }

                    if (targetSkill == null)
                    {
                        continue;
                    }

                    double skillBonus = 0;
                    if (e.ValueType == AttributeValueType.Percent)
                    {
                        if (value >= 1)
                        {
                            value = value / 100d;
                        }

                        skillBonus = (targetSkill.Level * value);
                    }
                    else
                    {
                        skillBonus = value;
                    }

                    result.Add(new SkillBonus { Enchantment = e, Bonus = skillBonus, Skill = targetSkill });
                }
            }
            return result;
        }

        public static IReadOnlyList<SkillBonus> GetSkillBonuses(this ReadOnlyInventoryItem i, Guid characterId, GameData gameData)
        {
            var character = gameData.GetCharacter(characterId);
            var skills = ModelMapper.MapForWebsite(gameData.GetCharacterSkills(character.SkillsId));
            var playerSkills = skills.AsList();
            return GetSkillBonuses(i, playerSkills, gameData);
        }

        public static Item GetItem(Guid itemId, GameData gameData)
        {
            var dataItem = gameData.GetItem(itemId);
            return ModelMapper.Map(gameData, dataItem);
        }

        public static IReadOnlyList<ItemStat> GetItemStats(this ReadOnlyInventoryItem item, GameData gameData)
        {
            return GetItemStats(item.ItemId, item.Enchantment, gameData);
        }

        public static IReadOnlyList<ItemStat> GetItemStats(this InventoryItem item, GameData gameData)
        {
            return GetItemStats(item.ItemId, item.Enchantment, gameData);
        }

        public static IReadOnlyList<ItemStat> GetItemStats(Guid itemId, string enchantment, GameData gameData)
        {
            var stats = new List<ItemStat>();
            var i = GetItem(itemId, gameData);
            if (i == null) return stats;

            int aimBonus = 0;
            int armorBonus = 0;
            int powerBonus = 0;

            if (!string.IsNullOrEmpty(enchantment))
            {
                var enchantments = enchantment.ToLower().Split(';');
                foreach (var e in enchantments)
                {
                    var value = PlayerInventory.GetValue(e, out var type);
                    var key = e.Split(':')[0];
                    if (type == AttributeValueType.Percent)
                    {
                        value = value / 100d;
                        if (key == "power") powerBonus = (int)(i.WeaponPower * value) + (int)(i.MagicPower * value) + (int)(i.RangedPower * value);
                        if (key == "aim") aimBonus = (int)(i.WeaponAim * value) + (int)(i.MagicAim * value) + (int)(i.RangedAim * value);
                        if (key == "armor" || key == "armour") armorBonus = (int)(i.ArmorPower * value);
                    }
                    else
                    {
                        if (key == "power") powerBonus = (int)value;
                        if (key == "aim") aimBonus = (int)value;
                        if (key == "armor" || key == "armour") armorBonus = (int)value;
                    }
                }
            }

            if (i.WeaponAim > 0) stats.Add(new ItemStat("Weapon Aim", i.WeaponAim, aimBonus));
            if (i.WeaponPower > 0) stats.Add(new ItemStat("Weapon Power", i.WeaponPower, powerBonus));
            if (i.RangedAim > 0) stats.Add(new ItemStat("Ranged Aim", i.RangedAim, aimBonus));
            if (i.RangedPower > 0) stats.Add(new ItemStat("Ranged Power", i.RangedPower, powerBonus));
            if (i.MagicAim > 0) stats.Add(new ItemStat("Magic Aim", i.MagicAim, aimBonus));
            if (i.MagicPower > 0) stats.Add(new ItemStat("Magic Power", i.MagicPower, powerBonus));
            if (i.ArmorPower > 0) stats.Add(new ItemStat("Armor", i.ArmorPower, armorBonus));

            return stats;
        }

        public static IReadOnlyList<ItemEnchantment> GetItemEnchantments(this InventoryItem item, GameData gameData)
        {
            var availableAttributes = gameData.GetItemAttributes();
            return GetItemEnchantments(item, availableAttributes);
        }

        public static IReadOnlyList<ItemEnchantment> GetItemEnchantments(this ReadOnlyInventoryItem item, IReadOnlyList<DataModels.ItemAttribute> availableAttributes)
        {
            return GetItemEnchantments(item.Enchantment, availableAttributes);
        }

        public static IReadOnlyList<ItemEnchantment> GetItemEnchantments(this InventoryItem item, IReadOnlyList<DataModels.ItemAttribute> availableAttributes)
        {
            return GetItemEnchantments(item.Enchantment, availableAttributes);
        }


        public static IReadOnlyList<ItemEnchantment> GetItemEnchantments(string enchantment, IReadOnlyList<DataModels.ItemAttribute> availableAttributes)
        {
            var enchantments = new List<ItemEnchantment>();
            if (!string.IsNullOrEmpty(enchantment))
            {
                var en = enchantment.Split(';');
                foreach (var e in en)
                {
                    var data = e.Split(':');
                    var key = data[0];
                    var value = PlayerInventory.GetValue(data[1], out var type);
                    var attr = availableAttributes.FirstOrDefault(x => x.Name == key);
                    var description = "";

                    if (type == AttributeValueType.Percent)
                    {
                        if (attr != null)
                        {
                            description = attr.Description.Replace(attr.MaxValue, value + "%");
                        }
                        value = value / 100d;
                    }
                    else
                    {
                        if (attr != null)
                        {
                            description = attr.Description.Replace(attr.MaxValue, value.ToString());
                        }
                    }

                    enchantments.Add(new ItemEnchantment
                    {
                        Name = key,
                        Value = value,
                        ValueType = type,
                        Description = description,
                    });
                }
            }
            return enchantments;
        }

    }
}
