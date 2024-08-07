﻿using System;

namespace RavenNest.Models
{
    public class MarketItem
    {
        public Guid Id { get; set; }
        public Guid SellerCharacterId { get; set; }
        public Guid SellerUserId { get; set; }
        public Guid ItemId { get; set; }
        public long Amount { get; set; }
        public double PricePerItem { get; set; }
        public string Tag { get; set; }
        public DateTime? Expires { get; set; }
        public DateTime? Created { get; set; }
        public string Enchantment { get; set; }
        public string Name { get; set; }
        public Guid? TransmogrificationId { get; set; }
        public int Flags { get; set; }
    }
}
