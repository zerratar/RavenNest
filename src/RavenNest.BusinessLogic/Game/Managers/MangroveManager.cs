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

        // the game's prices for meddling in the regatta are 500 and 2000 coins; nothing on the island costs more
        private const long MostSpent = 10_000;

        private readonly GameData gameData;
        private readonly PlayerInventoryProvider inventoryProvider;
        private readonly ILogger<MangroveManager> logger;
        private readonly BoatManager boats;
        private readonly ConcurrentDictionary<string, DateTime> lastPaid = new ConcurrentDictionary<string, DateTime>();

        /// <summary>Boat looks found digging (docs/own-boats.md in the game, "Customizing boats"): a flag, rarely a figurehead.</summary>
        private const double TreasureFlagChance = 0.12, TreasureFigureheadChance = 0.04;
        /// <summary>A regatta finisher other than the winner, now and then a sail too.</summary>
        private const double RegattaSailChance = 0.1;

        public MangroveManager(GameData gameData, PlayerInventoryProvider inventoryProvider, BoatManager boats, ILogger<MangroveManager> logger)
        {
            this.gameData = gameData;
            this.inventoryProvider = inventoryProvider;
            this.boats = boats;
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

                // boat looks: the pirates' own colours for holding their raid; a flag or figurehead found digging
                var looks = new List<string>();
                if (pirates)
                {
                    if (boats.GrantLook(character.UserId, BoatLooks.PirateSails, "pirates")) looks.Add(BoatLooks.PirateSails);
                    if (boats.GrantLook(character.UserId, BoatLooks.PirateFlag, "pirates")) looks.Add(BoatLooks.PirateFlag);
                }
                else
                {
                    if (rng.NextDouble() < TreasureFlagChance) looks.Add(boats.GrantNew(character.UserId, "flag", BoatLooks.TreasureFlags, "treasure", rng));
                    if (rng.NextDouble() < TreasureFigureheadChance) looks.Add(boats.GrantNew(character.UserId, "figurehead", BoatLooks.TreasureFigureheads, "treasure", rng));
                }
                reward.Looks = looks.Where(l => l != null).ToArray();
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
            var rng = Random.Shared;
            var place = 0;
            var ids = finishers.Distinct().Take(MostPaid).ToList();
            // a race takes two: alone (the streamer's forced start) it paid the winner's prize and sail every ten
            // minutes, so a lone finisher gets what the back of a race does
            var race = ids.Count(id => sessionCharacters.Any(x => x.Id == id)) >= 2;
            foreach (var id in ids)
            {
                var character = sessionCharacters.FirstOrDefault(x => x.Id == id);
                ++place;
                if (character == null) continue;
                var skills = gameData.GetCharacterSkills(character.SkillsId);
                var factor = !race ? 0.75 : place == 1 ? 4 : place == 2 ? 2.5 : place == 3 ? 1.5 : 0.75;
                var coins = (long)Math.Round((300 + (skills?.SailingLevel ?? 1) * 10) * factor / 10) * 10;
                var resources = gameData.GetResources(character);
                if (resources != null) resources.Coins += coins;
                // the regatta's sails: the winner one not yet owned, the others now and then
                var sail = race && (place == 1 || rng.NextDouble() < RegattaSailChance) ? boats.GrantNew(character.UserId, "sails", BoatLooks.RegattaSails, "regatta", rng) : null;
                rewards.Add(new MangroveReward { CharacterId = character.Id, Coins = coins, Items = new EventItemReward[0], Looks = sail != null ? new[] { sail } : new string[0] });
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

        /// <summary>
        /// Coins a character playing in the calling stream pays on the island (the game's regatta meddling: a powder keg
        /// or wind before the free one is back, or aimed at a racer). Returns the coins left, or -1 when they had too few.
        /// </summary>
        public long Spend(SessionToken session, Guid characterId, long coins)
        {
            if (coins <= 0 || coins > MostSpent) return -1;
            var character = gameData.GetCharacter(characterId);
            if (character == null || session == null || character.UserIdLock != session.UserId) return -1;
            var resources = gameData.GetResources(character);
            if (resources == null || resources.Coins < coins) return -1;
            resources.Coins -= coins;
            gameData.Touch();
            logger.LogInformation(character.Name + " paid " + coins + " coins to meddle in the regatta");
            return (long)resources.Coins;
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
