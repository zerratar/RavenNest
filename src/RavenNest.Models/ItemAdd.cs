﻿using System;
using System.Collections.Generic;

namespace RavenNest.Models
{
    public class ItemAdd
    {
        public Guid PlayerId { get; set; }
        public Guid ItemId { get; set; }
        public Guid InventoryItemId { get; set; }
        public long Amount { get; set; }
        public string Name { get; set; }
        public string Enchantment { get; set; }
        public Guid? TransmogrificationId { get; set; }
        public int Flags { get; set; }
        public string Tag { get; set; }
        public bool Soulbound { get; set; }
    }

    public class ItemEquip
    {
        public Guid PlayerId { get; set; }
        public Guid InventoryItemId { get; set; }
        public bool IsEquipped { get; set; }
    }

    public class ItemSync
    {
        public Guid PlayerId { get; set; }
        public List<InventoryItem> Items { get; set; }
    }
}
