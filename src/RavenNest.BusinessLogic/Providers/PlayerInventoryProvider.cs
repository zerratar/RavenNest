using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Providers;
using System;
using System.Collections.Concurrent;

public class PlayerInventoryProvider : IPlayerInventoryProvider
{
    private readonly ILogger<PlayerInventoryProvider> logger;
    private readonly IGameData gameData;
    private readonly ConcurrentDictionary<Guid, PlayerInventory> inventories
        = new ConcurrentDictionary<Guid, PlayerInventory>();

    public PlayerInventoryProvider(
        ILogger<PlayerInventoryProvider> logger,
        IGameData gameData)
    {
        this.logger = logger;
        this.gameData = gameData;
    }
    public PlayerInventory Get(Guid characterId)
    {
        if (inventories.TryGetValue(characterId, out var inventory))
        {
            return inventory;
        }
        return inventories[characterId] = new PlayerInventory(logger, gameData, characterId);
    }
}
