using System;

namespace RavenNest.DataModels
{
    public partial class InventoryItem : Entity<InventoryItem>
    {
        private Guid id; public Guid Id { get => id; set => Set(ref id, value); }
        private Guid characterId; public Guid CharacterId { get => characterId; set => Set(ref characterId, value); }
        private Guid itemId; public Guid ItemId { get => itemId; set => Set(ref itemId, value); }
        private long? amount; public long? Amount { get => amount; set => Set(ref amount, value); }
        private bool equipped; public bool Equipped { get => equipped; set => Set(ref equipped, value); }
        private string tag; public string Tag { get => tag; set => Set(ref tag, value); }
        private bool? soulbound; public bool? Soulbound { get => soulbound; set => Set(ref soulbound, value); }
    }
}
