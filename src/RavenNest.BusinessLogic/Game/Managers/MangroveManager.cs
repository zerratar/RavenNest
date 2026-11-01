using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.Models;
using RavenNest.Sessions;

namespace RavenNest.BusinessLogic.Game
{
    /// <summary>
    /// The Mangrove island (docs/banana-island.md in the game): the treasure dug up from a pirate's map, the chest the
    /// defenders share when the pirates' raid is held, the regatta's prizes, and the bananas viewers feed the monkeys.
    /// The game runs all of it and says who dug, who held and who finished where; the server only pays characters
    /// playing in the calling stream, and each kind of reward at most once in a while per stream.
    /// </summary>
    public class MangroveManager
    {
        public const string Treasure = "treasure";
        public const string Pirates = "pirates";

        // the game's own spacing: a map at a time, a raid at a time, a race at a time
        private static readonly TimeSpan ChestEvery = TimeSpan.FromSeconds(90);
        private static readonly TimeSpan RegattaEvery = TimeSpan.FromSeconds(120);

        // diggers and defenders paid per chest, finishers per race
        private const int MostPaid = 50;

        private readonly GameData gameData;
        private readonly PlayerInventoryProvider inventoryProvider;
        private readonly ILogger<MangroveManager> logger;
        private readonly ConcurrentDictionary<string, DateTime> lastPaid = new ConcurrentDictionary<string, DateTime>();

        public MangroveManager(GameData gameData, PlayerInventoryProvider inventoryProvider, ILogger<MangroveManager> logger)
        {
            this.gameData = gameData;
            this.inventoryProvider = inventoryProvider;
            this.logger = logger;
        }

        /// <summary>
        /// A chest for each character: coins by their combat level, and one roll on the island's treasure table. The
        /// pirates' chest (the raid held) pays a little more and rolls twice for the first one, who held the line
        /// longest.
        /// </summary>
        public MangroveReward[] OpenChest(SessionToken session, string kind, Guid[] characters)
        {
            if (characters == null || characters.Length == 0) return new MangroveReward[0];
            var pirates = string.Equals(kind, Pirates, StringComparison.OrdinalIgnoreCase);
            if (!pirates && !string.Equals(kind, Treasure, StringComparison.OrdinalIgnoreCase)) return new MangroveReward[0];
            var gameSession = gameData.GetSession(session.SessionId);
            if (gameSession == null || !Due(session.SessionId + kind, ChestEvery)) return new MangroveReward[0];

            var items = gameData.GetKnownItems();
            var sessionCharacters = gameData.GetActiveSessionCharacters(gameSession);
            var rng = Random.Shared;
            var rewards = new List<MangroveReward>();
            foreach (var id in characters.Distinct().Take(MostPaid))
            {
                var character = sessionCharacters.FirstOrDefault(x => x.Id == id);
                if (character == null) continue;
                var skills = gameData.GetCharacterSkills(character.SkillsId);
                var combat = skills == null ? 1 : (skills.AttackLevel + skills.DefenseLevel + skills.StrengthLevel + skills.HealthLevel) / 4;
                var coins = (long)Math.Round((300 + combat * 10) * (1 + rng.NextDouble()) * (pirates ? 1.25 : 1) / 10) * 10;
                var resources = gameData.GetResources(character);
                if (resources != null) resources.Coins += coins;

                var reward = new MangroveReward { CharacterId = character.Id, Coins = coins };
                var given = new List<EventItemReward>();
                var rolls = pirates && rewards.Count == 0 ? 2 : 1;
                for (var r = 0; r < rolls; ++r)
                {
                    var (item, amount) = Roll(items, rng);
                    if (item == null) continue;
                    var stack = inventoryProvider.Get(character.Id).AddItem(item.Id, amount).FirstOrDefault();
                    given.Add(new EventItemReward { CharacterId = character.Id, ItemId = item.Id, InventoryItemId = stack?.Id ?? Guid.Empty, Amount = amount });
                }
                reward.Items = given.ToArray();
                rewards.Add(reward);
            }
            gameData.Touch();
            return rewards.ToArray();
        }

        // the island's treasure table: mostly bananas, sometimes something the monkeys and pirates both want
        private static (DataModels.Item Item, int Amount) Roll(TypedItems items, Random rng)
        {
            var roll = rng.NextDouble();
            if (roll < 0.01) return (items.Pets.MonkeyPet, 1);
            if (roll < 0.03) return (items.Bananarang, 1);
            if (roll < 0.11) return (items.GoldenBanana, 1);
            if (roll < 0.30) return (items.BananaSplit, 1 + rng.Next(2));
            if (roll < 0.50) return (items.BananaBread, 2 + rng.Next(3));
            return (items.Banana, 3 + rng.Next(8));
        }

        /// <summary>
        /// The regatta's finishers, first to last: coins by place and Sailing level. The game gives the Sailing exp
        /// (it owns the skills); this returns the coins it paid, in the same order.
        /// </summary>
        public MangroveReward[] RegattaPrizes(SessionToken session, Guid[] finishers)
        {
            if (finishers == null || finishers.Length == 0) return new MangroveReward[0];
            var gameSession = gameData.GetSession(session.SessionId);
            if (gameSession == null || !Due(session.SessionId + "regatta", RegattaEvery)) return new MangroveReward[0];

            var sessionCharacters = gameData.GetActiveSessionCharacters(gameSession);
            var rewards = new List<MangroveReward>();
            var place = 0;
            foreach (var id in finishers.Distinct().Take(MostPaid))
            {
                var character = sessionCharacters.FirstOrDefault(x => x.Id == id);
                ++place;
                if (character == null) continue;
                var skills = gameData.GetCharacterSkills(character.SkillsId);
                var factor = place == 1 ? 4 : place == 2 ? 2.5 : place == 3 ? 1.5 : 0.75;
                var coins = (long)Math.Round((300 + (skills?.SailingLevel ?? 1) * 10) * factor / 10) * 10;
                var resources = gameData.GetResources(character);
                if (resources != null) resources.Coins += coins;
                rewards.Add(new MangroveReward { CharacterId = character.Id, Coins = coins, Items = new EventItemReward[0] });
            }
            gameData.Touch();
            return rewards.ToArray();
        }

        /// <summary>
        /// !banana feed: the character hands the monkeys up to this many of their own bananas ("golden": Golden
        /// Bananas); returns how many were taken. Soulbound stacks are taken too: the bananas are not traded, only eaten.
        /// </summary>
        public long Feed(SessionToken session, Guid characterId, string kind, long amount)
        {
            if (amount <= 0) return 0;
            var character = gameData.GetCharacter(characterId);
            if (character == null || session == null || character.UserIdLock != session.UserId) return 0;
            var items = gameData.GetKnownItems();
            var item = string.Equals(kind, "golden", StringComparison.OrdinalIgnoreCase) ? items.GoldenBanana : items.Banana;
            if (item == null) return 0;

            var inventory = inventoryProvider.Get(character.Id);
            var left = Math.Min(amount, 10_000);
            var taken = 0L;
            foreach (var stack in inventory.GetUnequippedItems().Where(s => s.ItemId == item.Id).ToList())
            {
                if (left <= 0) break;
                var take = Math.Min(stack.Amount, left);
                if (take > 0 && inventory.RemoveItem(stack, take))
                {
                    left -= take;
                    taken += take;
                }
            }
            if (taken > 0) logger.LogInformation(character.Name + " fed the monkeys " + taken + " " + item.Name);
            return taken;
        }

        private bool Due(string key, TimeSpan every)
        {
            var now = DateTime.UtcNow;
            if (lastPaid.TryGetValue(key, out var last) && now - last < every) return false;
            lastPaid[key] = now;
            return true;
        }
    }
}
