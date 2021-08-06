using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Providers;
using RavenNest.DataModels;
using RavenNest.Models;
using System;
using System.Linq;

namespace RavenNest.BusinessLogic.Game
{
    public class EnchantmentManager : IEnchantmentManager
    {
        private readonly ILogger<EnchantmentManager> logger;
        private readonly IGameData gameData;
        private readonly Random random;
        private const int MaximumEnchantmentCount = 10;

        public EnchantmentManager(ILogger<EnchantmentManager> logger, IGameData gameData)
        {
            this.logger = logger;
            this.gameData = gameData;
            this.random = new System.Random();
        }

        public ItemEnchantmentResult EnchantItem(
            DataModels.ClanSkill clanSkill,
            Character character,
            PlayerInventory inventory,
            ReadOnlyInventoryItem item,
            DataModels.Resources resources)
        {
            item = inventory.RemoveAttributes(item);

            var i = item.Item;
            var enchantable =
                // i.Type == (int)DataModels.ItemCategory.Pet || // in the future. :)
                i.Type == (int)DataModels.ItemCategory.Weapon ||
                i.Type == (int)DataModels.ItemCategory.Armor ||
                i.Type == (int)DataModels.ItemCategory.Ring ||
                i.Type == (int)DataModels.ItemCategory.Amulet;

            if (!enchantable)
            {
                return ItemEnchantmentResult.Failed;
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

            if (targetAttributeCount == 0)
            {
                return ItemEnchantmentResult.Failed;
            }

            var maxEnchantments = (int)Math.Floor(Math.Max(MaximumEnchantmentCount, Math.Floor((float)clanSkill.Level / 3f)));

            targetAttributeCount = Math.Max(0, Math.Min(maxEnchantments, targetAttributeCount));


            var invItem = inventory.Get(item.Id);
            if (invItem.IsNull())
            {
                return ItemEnchantmentResult.Failed;
            }

            var newAttributes = inventory.CreateRandomAttributes(invItem, targetAttributeCount);
            var id = item.Id;
            if (isStack)
            {
                inventory.RemoveItem(item, 1);
                invItem = inventory.AddItem(item.ItemId, 1, true, newAttributes).FirstOrDefault();
            }
            else
            {
                foreach (var attr in newAttributes)
                {
                    gameData.Add(attr);
                }
            }

            var exp = GameMath.GetEnchantingExperience(clanSkill.Level, targetAttributeCount, itemLvReq);
            var multiplier = gameData.GetActiveExpMultiplierEvent();
            if (multiplier != null)
            {
                exp = multiplier.Multiplier * exp;
            }

            // TODO: Add exp
            // TODO: Return inventory item

            return new ItemEnchantmentResult()
            {
                //InventoryItem = invItem
                Result = ItemEnchantmentResultValue.Success,
            };
        }
    }
}
