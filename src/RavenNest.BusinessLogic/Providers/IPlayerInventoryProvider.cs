using System;

namespace RavenNest.BusinessLogic.Providers
{
    public interface IPlayerInventoryProvider
    {
        PlayerInventory Get(Guid characterId);
    }
}
