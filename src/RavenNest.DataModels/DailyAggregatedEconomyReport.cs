using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    public partial class DailyAggregatedEconomyReport : Entity<DailyAggregatedEconomyReport>
    {
        /// <summary>
        /// The date of the report.
        /// </summary>
        [PersistentData] private DateTime date;

        /// <summary>
        /// The average amount of coins per player.
        /// </summary>
        [PersistentData] private double avgCoinsPerPlayer;

        /// <summary>
        /// The total amount of coins in the game.
        /// </summary>
        [PersistentData] private long totalCoins;

        /// <summary>
        /// The total number of players in the game.
        /// </summary>
        [PersistentData] private int totalPlayers;

        /// <summary>
        /// The number of players who were active during the day.
        /// </summary>
        [PersistentData] private int activePlayers;

        /// <summary>
        /// The number of new players who joined the game during the day.
        /// </summary>
        [PersistentData] private int newPlayers;

        /// <summary>
        /// The total number of items sold in the game during the day.
        /// </summary>
        [PersistentData] private long itemsSold;

        /// <summary>
        /// The total number of items sold to the marketplace during the day.
        /// </summary>
        [PersistentData] private long itemsSoldToMarketplace;

        /// <summary>
        /// The total number of items sold to NPC merchants during the day.
        /// </summary>
        [PersistentData] private long itemsSoldToNpc;

        /// <summary>
        /// The number of unique items sold in the game during the day.
        /// </summary>
        [PersistentData] private long uniqueItemsSold;

        /// <summary>
        /// The total number of transactions in the game during the day.
        /// </summary>
        [PersistentData] private long totalTransactions;

        /// <summary>
        /// The total amount of coins spent by players during the day.
        /// </summary>        
        [PersistentData] private long totalCoinsSpent;

        /// <summary>
        /// The total amount of coins spent on purchasing items during the day.
        /// </summary>
        [PersistentData] private long coinsSpentOnItems;

        /// <summary>
        /// The total amount of coins spent on in-game services during the day.
        /// </summary>
        [PersistentData] private long coinsSpentOnServices;

        /// <summary>
        /// The total amount of coins earned by players from completing quests during the day.
        /// </summary>        
        [PersistentData] private long coinsGainedFromQuests;

        /// <summary>
        /// The total amount of coins earned by players from various in-game activities during the day.
        /// </summary>        
        [PersistentData] private long coinsGainedFromActivities;

        /// <summary>
        /// The total amount of coins earned by players from trading items during the day.
        /// </summary>
        [PersistentData] private long coinsGainedFromTrading;
    }
}
