using System;
using System.Collections.Generic;

namespace RavenNest.Models
{
    // Want to rename this to ItemInstance
    public class InventoryItem
    {
        public Guid Id { get; set; }

        public Guid ItemId { get; set; }
        public long Amount { get; set; }
        public bool Equipped { get; set; }
        public string Tag { get; set; }
        public bool? Soulbound { get; set; }
        public string Enchantment { get; set; }
        public string Name { get; set; }
        public Guid? TransmogrificationId { get; set; }
        public int Flags { get; set; }

        // TODO: Enable attributes
        //public InventoryItemAttribute[] Attributes { get; set; }
    }

    public class UserBankItem
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid ItemId { get; set; }
        public long Amount { get; set; }
        public string Name { get; set; }
        public string Enchantment { get; set; }
        public string Tag { get; set; }
        public bool Soulbound { get; set; }
        public Guid? TransmogrificationId { get; set; }
        public int Flags { get; set; }
    }

    public class InventoryItemAttribute
    {
        public Guid Id { get; set; }
        public Guid InventoryItemId { get; set; }
        public ItemAttribute Attribute { get; set; }
        public string Value { get; set; }
    }

    public class ItemAttribute
    {
        public int AttributeIndex { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int Type { get; set; }
        public string MaxValue { get; set; }
        public string MinValue { get; set; }
        public string DefaultValue { get; set; }

    }
}
