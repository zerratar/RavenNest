using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Extensions;
using RavenNest.BusinessLogic.Game.Processors.Tasks;
using RavenNest.Combat;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    /// <summary>
    ///     !use message in a bottle and !use treasure map (v0.9.7, the game's docs/own-boats.md, "Messages in bottles"):
    ///     the server opens the bottle, takes it, and says which note was inside or puts a Treasure Map in the bag; it
    ///     checks the map's island against where it last heard the character was, takes the map and gives the treasure.
    ///     The rolls are in <see cref="Bottles"/>; the game words every answer.
    /// </summary>
    public class BottleManager
    {
        /// <summary>The achievement (hidden, one tier) whose title is "the Treasure Hunter".</summary>
        public const string TreasureAchievement = "treasure";

        private readonly GameData gameData;
        private readonly PlayerInventoryProvider inventoryProvider;
        private readonly BoatManager boats;
        private readonly ILogger<BottleManager> logger;

        public BottleManager(GameData gameData, PlayerInventoryProvider inventoryProvider, BoatManager boats, ILogger<BottleManager> logger)
        {
            this.gameData = gameData;
            this.inventoryProvider = inventoryProvider;
            this.boats = boats;
            this.logger = logger;
        }

        /// <summary>Whether this item is one of these two; the caller hands it to <see cref="Use"/> then.</summary>
        public bool Handles(DataModels.Item item)
        {
            if (item == null) return false;
            var items = gameData.GetKnownItems();
            return item.Id == items.MessageInABottle?.Id || item.Id == items.TreasureMap?.Id;
        }

        /// <summary>Uses a bottle or a map of the character's, in the calling session. Null when nothing could be done.</summary>
        public ItemUseResult Use(DataModels.GameSession session, DataModels.Character character, Guid inventoryItemId, Random rng = null)
        {
            rng ??= Random.Shared;
            var inventory = inventoryProvider.Get(character.Id);
            var item = inventory.Get(inventoryItemId);
            if (item.IsNull() || item.Amount <= 0) return null;

            var items = gameData.GetKnownItems();
            if (item.ItemId == items.MessageInABottle?.Id) return OpenBottle(session, character, inventory, item, inventoryItemId, rng);
            if (item.ItemId == items.TreasureMap?.Id) return Dig(session, character, inventory, item, inventoryItemId, rng);
            return null;
        }

        // ---- The bottle ----------------------------------------------------------------------------------------

        private ItemUseResult OpenBottle(DataModels.GameSession session, DataModels.Character character, PlayerInventory inventory,
            ReadOnlyInventoryItem bottle, Guid inventoryItemId, Random rng)
        {
            var map = gameData.GetKnownItems().TreasureMap;
            var contents = Bottles.Open(rng);
            if (contents.Map && map == null) contents = Bottles.Open(1, rng.NextDouble(), rng.NextDouble()); // no map item: a note then

            if (!inventory.RemoveItem(bottle, 1)) return null;
            var result = Result(inventory, inventoryItemId);

            if (!contents.Map)
            {
                result.Outcome = "note";
                result.Note = contents.Note;
                return result;
            }

            var island = Bottles.PickIsland(Reachable(session, character), rng.NextDouble());
            var stack = inventory.AddItem(map.Id, 1, tag: Bottles.TagOf(island)).FirstOrDefault();
            result.Outcome = "map";
            result.EffectIsland = island;
            result.Items = new List<EventItemReward>
            {
                new EventItemReward { CharacterId = character.Id, ItemId = map.Id, InventoryItemId = stack?.Id ?? Guid.Empty, Amount = 1 }
            };
            logger.LogInformation(character.Name + " found a treasure map for " + island + " in a bottle");
            return result;
        }

        private List<Island> Reachable(DataModels.GameSession session, DataModels.Character character)
        {
            var skills = gameData.GetCharacterSkills(character.SkillsId);
            var mangroveOpen = ResourceTaskProcessor.IslandOpen(gameData, session, Island.Mangrove);
            return Bottles.Reachable(s => skills?.GetSkill((int)s)?.Level ?? 1, mangroveOpen);
        }

        // ---- The map -------------------------------------------------------------------------------------------

        private ItemUseResult Dig(DataModels.GameSession session, DataModels.Character character, PlayerInventory inventory,
            ReadOnlyInventoryItem map, Guid inventoryItemId, Random rng)
        {
            var island = Bottles.IslandOf(map.Tag);
            if (island == Island.None)
            {
                // a map without an island (given by hand): the same one every time, from the stack's id
                var reachable = Reachable(session, character);
                island = reachable[(int)((uint)map.Id.GetHashCode() % (uint)reachable.Count)];
            }

            var result = Result(inventory, inventoryItemId);
            result.EffectIsland = island;

            if (!ResourceTaskProcessor.IslandOpen(gameData, session, island))
            {
                result.Outcome = "locked";
                return result;
            }

            var state = gameData.GetCharacterState(character.StateId);
            if (state == null || state.InRaid || state.InArena || state.InDungeon.GetValueOrDefault() || state.InOnsen.GetValueOrDefault())
            {
                return null; // busy: the game says so before asking
            }

            if (!string.Equals(state.Island, island.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                result.Outcome = "elsewhere";
                return result;
            }

            if (!inventory.RemoveItem(map, 1)) return null;
            result = Result(inventory, inventoryItemId);
            result.EffectIsland = island;
            result.Outcome = "treasure";

            var skills = gameData.GetCharacterSkills(character.SkillsId);
            var level = Bottles.LevelOf(s => skills?.GetSkill((int)s)?.Level ?? 1);
            var treasure = Bottles.Roll(level, rng);
            var found = new List<EventItemReward>();

            var coins = treasure.Coins;
            if (treasure.Memorable && !Memorable(character, result, rng)) coins *= 2; // nothing left to remember: twice the coins
            var resources = gameData.GetResources(character);
            if (resources != null)
            {
                resources.Coins += coins;
                result.Coins = coins;
            }

            if (treasure.Gem != null)
            {
                var (kind, tier) = treasure.Gem.Value;
                Give(character, inventory, Gems.ItemId(kind, tier), found);
            }

            if (treasure.Egg)
            {
                var species = PetEggs.Pick(EggSource.PirateRaid, 0, rng.NextDouble());
                var stackId = PetEggRewards.Give(gameData, inventoryProvider, character.Id, species);
                if (stackId != Guid.Empty)
                    found.Add(new EventItemReward { CharacterId = character.Id, ItemId = PetEggs.ItemId(species.Id), InventoryItemId = stackId, Amount = 1 });
            }

            if (treasure.Bottle) Give(character, inventory, gameData.GetKnownItems().MessageInABottle?.Id ?? Guid.Empty, found);

            result.Items = found;
            gameData.Touch();
            logger.LogInformation(character.Name + " dug up a treasure on " + island + ": " + coins + " coins, " + found.Count + " items"
                + (result.Look != null ? ", the look " + result.Look : ""));
            return result;
        }

        /// <summary>
        ///     The rare find: the Treasure Hunter title, or a boat flag or figurehead from the treasure set, whichever the
        ///     character does not have yet (in a random order). False when they have them all.
        /// </summary>
        private bool Memorable(DataModels.Character character, ItemUseResult result, Random rng)
        {
            var titleFirst = rng.NextDouble() < 0.5;
            for (var i = 0; i < 2; ++i)
            {
                if ((i == 0) == titleFirst)
                {
                    if (HasTitle(character) || QuestTracker.Achievements == null) continue;
                    QuestTracker.Achievements.Set(character, TreasureAchievement, 1); // the game hears it as a secret achievement
                    if (HasTitle(character)) return true;
                }
                else
                {
                    var look = rng.NextDouble() < 2.0 / 3
                        ? boats.GrantNew(character.UserId, "flag", BoatLooks.TreasureFlags, "treasure", rng) ?? boats.GrantNew(character.UserId, "figurehead", BoatLooks.TreasureFigureheads, "treasure", rng)
                        : boats.GrantNew(character.UserId, "figurehead", BoatLooks.TreasureFigureheads, "treasure", rng) ?? boats.GrantNew(character.UserId, "flag", BoatLooks.TreasureFlags, "treasure", rng);
                    if (look == null) continue;
                    result.Look = look;
                    return true;
                }
            }
            return false;
        }

        private bool HasTitle(DataModels.Character character) =>
            gameData.GetCharacterAchievements(character.Id).Any(a => a.Key == TreasureAchievement && a.Tier >= 1);

        private void Give(DataModels.Character character, PlayerInventory inventory, Guid itemId, List<EventItemReward> found)
        {
            if (itemId == Guid.Empty || gameData.GetItem(itemId) == null) return;
            var stack = inventory.AddItem(itemId, 1).FirstOrDefault();
            found.Add(new EventItemReward { CharacterId = character.Id, ItemId = itemId, InventoryItemId = stack?.Id ?? Guid.Empty, Amount = 1 });
        }

        private static ItemUseResult Result(PlayerInventory inventory, Guid inventoryItemId)
        {
            return new ItemUseResult
            {
                InventoryItemId = inventoryItemId,
                NewStackAmount = (int)inventory.Get(inventoryItemId).Amount, // 0 when the stack is gone
                Effects = new List<RavenNest.Models.CharacterStatusEffect>(),
                EffectIsland = Island.None,
            };
        }
    }
}
