
using System;

namespace RavenNest.Models
{
    public class ItemEnchantmentResult
    {
        public static ItemEnchantmentResult Failed { get; } = new ItemEnchantmentResult();
        public ItemEnchantmentResultValue Result { get; set; }
        public bool Success => Result == ItemEnchantmentResultValue.Success;
        public InventoryItem InventoryItem { get; set; }
    }


    public enum ItemEnchantmentResultValue
    {
        Success,
        NotEnchantable,
        Error,
    }
}
