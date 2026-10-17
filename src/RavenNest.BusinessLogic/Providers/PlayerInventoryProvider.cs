using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.BusinessLogic.Providers;
using System;
using System.Collections.Concurrent;

public class PlayerInventoryProvider
{
    private readonly ILogger<PlayerInventoryProvider> logger;
    private readonly GameData gameData;
    // Lazy, so two threads asking at once share one inventory: the old check-then-set could build two for one
    // character, each with its own lock and its own view of the stacks, so an add through one was invisible to the other
    private readonly ConcurrentDictionary<Guid, Lazy<PlayerInventory>> inventories
        = new ConcurrentDictionary<Guid, Lazy<PlayerInventory>>();

    public PlayerInventoryProvider(
        ILogger<PlayerInventoryProvider> logger,
        GameData gameData)
    {
        this.logger = logger;
        this.gameData = gameData;
    }
    public PlayerInventory Get(Guid characterId)
    {
        return inventories.GetOrAdd(characterId,
            id => new Lazy<PlayerInventory>(() => new PlayerInventory(logger, gameData, id), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }
}
