using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Providers;
using RavenNest.DataModels;
using RavenNest.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RavenNest.BusinessLogic.Game
{
    public class EnchantmentManager : IEnchantmentManager
    {
        private readonly ILogger<EnchantmentManager> logger;
        private readonly IGameData gameData;
        private readonly Random random;
        private const int MaximumEnchantmentCount = 10;
        private const double MinEnchantmentInterval = 30;
        private const double MaxEnchantmentInterval = 60;
        public EnchantmentManager(ILogger<EnchantmentManager> logger, IGameData gameData)
        {
            this.logger = logger;
            this.gameData = gameData;
            this.random = new System.Random();
        }

        public ItemEnchantmentResult DisenchantItem(
            Guid sessionId,
            Character character,
            PlayerInventory inventory,
            ReadOnlyInventoryItem item)
        {
            var invItem = inventory.Get(item);
            if (invItem == null)
            {
                return ItemEnchantmentResult.Error();
            }


            if (string.IsNullOrEmpty(invItem.Enchantment))
            {
                return ItemEnchantmentResult.Error();
            }


            if (inventory.RemoveItem(item, 1))
            {
                var disenchantedItem = inventory.AddItem(item.ItemId, amount: 1, equipped: item.Equipped).FirstOrDefault();
                return new ItemEnchantmentResult
                {
                    EnchantedItem = DataMapper.Map<RavenNest.Models.InventoryItem, DataModels.InventoryItem>(disenchantedItem),
                    OldItemStack = DataMapper.Map<RavenNest.Models.InventoryItem, DataModels.InventoryItem>(invItem),
                    Result = ItemEnchantmentResultValue.Success
                };
            }

            return ItemEnchantmentResult.Error();
        }

        public ItemEnchantmentResult EnchantItem(
            System.Guid sessionId,
            DataModels.ClanSkill clanSkill,
            Character character,
            PlayerInventory inventory,
            ReadOnlyInventoryItem item,
            DataModels.Resources resources)
        {
            var user = gameData.GetUser(character.UserId);

            var invItem = inventory.Get(item);
            if (invItem == null)
            {
                // No such item in our inventory
                return ItemEnchantmentResult.Error();
            }

            var i = item.Item;
            var enchantable =
                // i.Type == (int)DataModels.ItemCategory.Pet || // in the future. :)
                i.Category == (int)DataModels.ItemCategory.Weapon ||
                i.Category == (int)DataModels.ItemCategory.Armor ||
                i.Category == (int)DataModels.ItemCategory.Ring ||
                i.Category == (int)DataModels.ItemCategory.Amulet;

            if (!enchantable)
            {
                return ItemEnchantmentResult.NotEnchantable;
            }

            var cd = gameData.GetClanSkillCooldown(character.Id, clanSkill.Id);

            //var characterSessionState = gameData.GetCharacterSessionState(sessionId, character.Id);
            if (cd.CooldownEnd > DateTime.MinValue && cd.CooldownEnd > DateTime.UtcNow)
            {
                return ItemEnchantmentResult.NotReady(cd.CooldownEnd);
            }

            var itemLvReq = (i.RequiredAttackLevel + i.RequiredDefenseLevel + i.RequiredMagicLevel + i.RequiredRangedLevel + i.RequiredSlayerLevel);
            var isStack = item.Amount > 1;

            var itemPercent = itemLvReq / (float)GameMath.MaxLevel;
            var itemMaxAttrCount = Math.Max(1, (int)Math.Floor(Math.Floor(itemLvReq / 10f) / 5));
            var success = clanSkill.Level / (float)itemLvReq;
            var targetAttributeCount = 0;
            var rng = random.NextDouble();
            if (rng <= success)
            {
                targetAttributeCount = Math.Max(1, itemMaxAttrCount);
            }
            else if (rng <= success * 1.33f)
            {
                targetAttributeCount = Math.Max(1, (int)Math.Floor(itemMaxAttrCount * 0.5f));
            }
            else if (rng <= success * 2f)
            {
                targetAttributeCount = Math.Max(1, (int)Math.Floor(itemMaxAttrCount * 0.33f));
            }
            else if (rng >= 0.75f)
            {
                targetAttributeCount = 1;
            }

            var maxEnchantments = (int)Math.Floor(Math.Max(MaximumEnchantmentCount, Math.Floor((float)clanSkill.Level / 3f)));

            targetAttributeCount = Math.Max(0, Math.Min(maxEnchantments, targetAttributeCount));

            if (targetAttributeCount == 0)
            {
                cd.CooldownStart = DateTime.UtcNow;
                cd.CooldownEnd = GetCooldown(user, success, 0.1d);
                return ItemEnchantmentResult.Failed(cd.CooldownEnd);
            }

            DataModels.InventoryItem enchantedItem = null;
            if (isStack)
            {
                inventory.RemoveItem(item, 1);
                enchantedItem = inventory.AddItemStack(item, 1);
            }
            else
            {
                enchantedItem = invItem;
            }

            var enchantmentAttributes = inventory.CreateRandomAttributes(enchantedItem, targetAttributeCount);
            enchantedItem.Soulbound = true;
            enchantedItem.Enchantment = FormatEnchantment(enchantmentAttributes);

            var itemName = gameData.GetItem(item.ItemId)?.Name;

            // Really stupid naming right now.
            enchantedItem.Name = GetEnchantedName(itemName, enchantmentAttributes);

            var multiplier = gameData.GetActiveExpMultiplierEvent()?.Multiplier ?? 1d;
            var gainedExp = GameMath.GetEnchantingExperience(clanSkill.Level, targetAttributeCount, itemLvReq) * multiplier;

            // 1. Add exp whenever user successefully enchants an item

            clanSkill.Experience += gainedExp;

            var gainedLevels = 0;
            var nextLevel = GameMath.ExperienceForLevel(clanSkill.Level + 1);
            while (gainedExp >= nextLevel)
            {
                gainedExp -= nextLevel;
                nextLevel = GameMath.ExperienceForLevel(clanSkill.Level + 1);
                clanSkill.Level = clanSkill.Level + 1;
                ++gainedLevels;
            }

            // TODO: 2. Send exp update to clients where players in same clan is regarding current state of the clan skill
            // Set a time limit on how often/frequently a player can use enchanting after they have enchanted an item.
            // Success: add n Hours based on what type of item
            // Fail: Add 10% of n Hours based on what type of item

            cd.CooldownStart = DateTime.UtcNow;
            cd.CooldownEnd = GetCooldown(user, success);

            return new ItemEnchantmentResult()
            {
                GainedExperience = gainedExp,
                GainedLevels = gainedLevels,
                EnchantedItem = DataMapper.Map<RavenNest.Models.InventoryItem, DataModels.InventoryItem>(enchantedItem),
                OldItemStack = DataMapper.Map<RavenNest.Models.InventoryItem, DataModels.InventoryItem>(invItem),
                Result = ItemEnchantmentResultValue.Success,
                Cooldown = cd.CooldownEnd
            };
        }

        private string GetEnchantedName(string itemName, List<MagicItemAttribute> attributes)
        {
            var highestValueAttribute = attributes.OrderByDescending(x => x.DoubleValue).FirstOrDefault();
            var attrName = highestValueAttribute.Attribute.Name.ToLower();

            attrName = char.ToUpper(attrName[0]) + attrName.Substring(1);

            var totalPlus = attributes.Sum(x => (int)Math.Floor(x.DoubleValue));

            var outputName = itemName + " of " + attrName + " +" + totalPlus;

            //var rank = attributes.Count > 1 ? " +" + (attributes.Count - 1) : "";

            return outputName;
        }

        private static DateTime GetCooldown(DataModels.User user, float random, double scale = 1d)
        {
#if DEBUG
            if (user != null && (user.IsAdmin.GetValueOrDefault() || user.IsModerator.GetValueOrDefault()))
            {
                //random *= 0.f;
                scale *= 0.5f;
                return DateTime.UtcNow.AddSeconds(Math.Min(random * MaxEnchantmentInterval, MinEnchantmentInterval) * scale);
            }
#endif

            return DateTime.UtcNow.AddMinutes(Math.Min(random * MaxEnchantmentInterval, MinEnchantmentInterval) * scale);
        }

        private static string FormatEnchantment(List<MagicItemAttribute> magicItemAttributes)
        {
            return String.Join(";", magicItemAttributes.Select(x => x.Attribute.Name + ":" + x.Value.ToString().Replace(',', '.')));
        }
    }
}
