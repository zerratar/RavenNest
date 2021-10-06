using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Providers;
using System;
using System.Collections.Concurrent;

public class PlayerInventoryProvider : IPlayerInventoryProvider
{
    private readonly IGameData gameData;
    private readonly ConcurrentDictionary<Guid, PlayerInventory> inventories
        = new ConcurrentDictionary<Guid, PlayerInventory>();

    public PlayerInventoryProvider(IGameData gameData)
    {
        this.gameData = gameData;
    }
    public PlayerInventory Get(Guid characterId)
    {
        if (inventories.TryGetValue(characterId, out var inventory))
        {
            return inventory;
        }
        return inventories[characterId] = new PlayerInventory(gameData, characterId);
    }
}
