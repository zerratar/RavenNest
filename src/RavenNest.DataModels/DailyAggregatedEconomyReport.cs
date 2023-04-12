using System;

namespace RavenNest.DataModels
{
    public partial class DailyAggregatedEconomyReport : Entity<DailyAggregatedEconomyReport>
    {
        private DateTime date;
        /// <summary>
        /// The date of the report.
        /// </summary>
        public DateTime Date
        {
            get => date;
            set => Set(ref date, value);
        }

        private double avgCoinsPerPlayer;
        /// <summary>
        /// The average amount of coins per player.
        /// </summary>
        public double AvgCoinsPerPlayer
        {
            get => avgCoinsPerPlayer;
            set => Set(ref avgCoinsPerPlayer, value);
        }

        private long totalCoins;
        /// <summary>
        /// The total amount of coins in the game.
        /// </summary>
        public long TotalCoins
        {
            get => totalCoins;
            set => Set(ref totalCoins, value);
        }

        private int totalPlayers;
        /// <summary>
        /// The total number of players in the game.
        /// </summary>
        public int TotalPlayers
        {
            get => totalPlayers;
            set => Set(ref totalPlayers, value);
        }

        private int activePlayers;
        /// <summary>
        /// The number of players who were active during the day.
        /// </summary>
        public int ActivePlayers
        {
            get => activePlayers;
            set => Set(ref activePlayers, value);
        }

        private int newPlayers;
        /// <summary>
        /// The number of new players who joined the game during the day.
        /// </summary>
        public int NewPlayers
        {
            get => newPlayers;
            set => Set(ref newPlayers, value);
        }

        private long itemsSold;
        /// <summary>
        /// The total number of items sold in the game during the day.
        /// </summary>
        public long ItemsSold
        {
            get => itemsSold;
            set => Set(ref itemsSold, value);
        }

        private long itemsSoldToMarketplace;
        /// <summary>
        /// The total number of items sold to the marketplace during the day.
        /// </summary>
        public long ItemsSoldToMarketplace
        {
            get => itemsSoldToMarketplace;
            set => Set(ref itemsSoldToMarketplace, value);
        }

        private long itemsSoldToNpc;
        /// <summary>
        /// The total number of items sold to NPC merchants during the day.
        /// </summary>
        public long ItemsSoldToNpc
        {
            get => itemsSoldToNpc;
            set => Set(ref itemsSoldToNpc, value);
        }

        private long uniqueItemsSold;
        /// <summary>
        /// The number of unique items sold in the game during the day.
        /// </summary>
        public long UniqueItemsSold
        {
            get => uniqueItemsSold;
            set => Set(ref uniqueItemsSold, value);
        }

        private long totalTransactions;
        /// <summary>
        /// The total number of transactions in the game during the day.
        /// </summary>
        public long TotalTransactions
        {
            get => totalTransactions;
            set => Set(ref totalTransactions, value);
        }

        private long totalCoinsSpent;
        /// <summary>
        /// The total amount of coins spent by players during the day.
        /// </summary>
        public long TotalCoinsSpent
        {
            get => totalCoinsSpent;
            set => Set(ref totalCoinsSpent, value);
        }

        private long coinsSpentOnItems;
        /// <summary>
        /// The total amount of coins spent on purchasing items during the day.
        /// </summary>
        public long CoinsSpentOnItems
        {
            get => coinsSpentOnItems;
            set => Set(ref coinsSpentOnItems, value);
        }

        private long coinsSpentOnServices;
        /// <summary>
        /// The total amount of coins spent on in-game services during the day.
        /// </summary>
        public long CoinsSpentOnServices
        {
            get => coinsSpentOnServices;
            set => Set(ref coinsSpentOnServices, value);
        }

        private long coinsGainedFromQuests;
        /// <summary>
        /// The total amount of coins earned by players from completing quests during the day.
        /// </summary>
        public long CoinsGainedFromQuests
        {
            get => coinsGainedFromQuests;
            set => Set(ref coinsGainedFromQuests, value);
        }

        private long coinsGainedFromActivities;
        /// <summary>
        /// The total amount of coins earned by players from various in-game activities during the day.
        /// </summary>
        public long CoinsGainedFromActivities
        {
            get => coinsGainedFromActivities;
            set => Set(ref coinsGainedFromActivities, value);
        }

        private long coinsGainedFromTrading;
        /// <summary>
        /// The total amount of coins earned by players from trading items during the day.
        /// </summary>
        public long CoinsGainedFromTrading
        {
            get => coinsGainedFromTrading;
            set => Set(ref coinsGainedFromTrading, value);
        }
    }
}
