
using System;

namespace RavenNest.Models
{
    public class ItemEnchantmentResult
    {
        public static ItemEnchantmentResult Failed { get; } = new ItemEnchantmentResult();
        public static ItemEnchantmentResult NotEnchantable { get; } = new ItemEnchantmentResult { Result = ItemEnchantmentResultValue.NotEnchantable };
        public static ItemEnchantmentResult NotReady(DateTime cooldown) => new ItemEnchantmentResult
        {
            Result = ItemEnchantmentResultValue.NotReady,
            Cooldown = cooldown
        };

        public ItemEnchantmentResultValue Result { get; set; }
        public bool Success => Result == ItemEnchantmentResultValue.Success;
        public InventoryItem EnchantedItem { get; set; }
        public InventoryItem OldItemStack { get; set; }
        public DateTime? Cooldown { get; set; }
        public int GainedLevels { get; set; }
        public double GainedExperience { get; set; }
    }


    public enum ItemEnchantmentResultValue
    {
        Success,
        NotEnchantable,
        Error,
        NotReady
    }
}
