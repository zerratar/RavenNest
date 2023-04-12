using RavenNest.DataModels;
using System;
using System.Collections.Generic;

namespace RavenNest.BusinessLogic.Data.Aggregators
{
    public class EconomyReportAggregator : DataAggregator
    {
        public EconomyReportAggregator(GameData gameData)
            : base(gameData, TimeSpan.FromHours(2), TimeSpan.FromDays(30), TimeSpan.FromDays(1))
        {
        }

        protected override void AggregateReport()
        {
            RemoveOldReports();

            var now = DateTime.UtcNow.Date;

            long coinsGainedFromActivities = 0;
            long coinsGainedFromQuests = 0;
            long coinsGainedFromTrading = 0;
            long coinsSpentOnItems = 0;
            long coinsSpentOnServices = 0;
            long itemsSold = 0;
            long itemsSoldToMarketPlace = 0;
            long itemsVendored = 0;
            long totalCoinsSpent = 0;
            long totalTransactions = 0;

            var totalCoins = 0L;
            var newPlayers = 0;
            var playerCount = 0;
            var characterCount = 0;
            int activePlayers = 0;

            var oneDayAgo = now.AddDays(-1);
            var users = gameData.GetUsers();

            foreach (var user in users)
            {
                var characters = gameData.GetCharactersByUserId(user.Id);
                var isActivePlayer = false;

                playerCount++;

                if (user.Created >= oneDayAgo)
                {
                    newPlayers++;
                }

                foreach (var c in characters)
                {
                    var resources = gameData.GetResources(c.ResourcesId);
                    // this should not happen, but if it does I don't want it to cause the report to crash.
                    if (resources == null)
                        continue;

                    totalCoins += (long)resources.Coins;

                    if (c.LastUsed >= oneDayAgo || c.UserIdLock != null)
                    {
                        isActivePlayer = true;
                    }

                    characterCount++;
                }

                if (isActivePlayer)
                {
                    activePlayers++;
                }
            }

            var vendorTransactions = gameData.GetVendorTransactions(oneDayAgo, now);
            var uniqueVendorItemsSold = new HashSet<Guid>();
            foreach (var transaction in vendorTransactions)
            {
                if (transaction.TransactionType)
                {
                    // something was bought

                    totalCoinsSpent += transaction.TotalPrice;
                    coinsSpentOnItems += transaction.TotalPrice;
                }
                else
                {
                    // something was sold
                    itemsSold += transaction.Amount;
                    itemsVendored += itemsVendored;
                    coinsGainedFromTrading += transaction.TotalPrice;
                    uniqueVendorItemsSold.Add(transaction.ItemId);
                }

                totalTransactions++;
            }

            var marketTransactions = gameData.GetMarketItemTransactions(oneDayAgo, now);
            var uniqueMarketplaceItemsSold = new HashSet<Guid>();
            foreach (var transaction in marketTransactions)
            {
                uniqueMarketplaceItemsSold.Add(transaction.ItemId);

                itemsSold += transaction.Amount;
                itemsSoldToMarketPlace += transaction.Amount;

                coinsGainedFromTrading += (long)transaction.TotalPrice;
                coinsSpentOnItems += (long)transaction.TotalPrice;
                totalCoinsSpent += (long)transaction.TotalPrice;

                totalTransactions++;
            }

            var uniqueItemsSold = uniqueMarketplaceItemsSold.Count + uniqueVendorItemsSold.Count;

            var data = new DailyAggregatedEconomyReport
            {
                Id = Guid.NewGuid(),
                Date = now,
                ActivePlayers = activePlayers,
                TotalCoins = totalCoins,
                AvgCoinsPerPlayer = totalCoins / characterCount,
                NewPlayers = newPlayers,
                TotalPlayers = playerCount,
                CoinsGainedFromActivities = coinsGainedFromActivities,
                CoinsGainedFromQuests = coinsGainedFromQuests,
                CoinsGainedFromTrading = coinsGainedFromTrading,
                CoinsSpentOnItems = coinsSpentOnItems,
                CoinsSpentOnServices = coinsSpentOnServices,
                ItemsSold = itemsSold,
                ItemsSoldToMarketplace = itemsSoldToMarketPlace,
                ItemsSoldToNpc = itemsVendored,
                TotalCoinsSpent = totalCoinsSpent,
                TotalTransactions = totalTransactions,
                UniqueItemsSold = uniqueItemsSold,
            };

            gameData.Add(data);
        }

        protected override void RemoveOldReports()
        {
            var currentDate = DateTime.UtcNow.Date;
            var retentionDate = currentDate - retentionTime;
            var oldReports = gameData.GetEconomyReports(DateTime.MinValue, retentionDate);
            foreach (var oldReport in oldReports)
            {
                gameData.Remove(oldReport);
            }

            var transactionRetentionDays = TimeSpan.FromDays(60);
            var transactionEndDate = currentDate.Subtract(transactionRetentionDays);

            // We should also clear out old vendor and marketplace transactions older than 90 days.
            var vendorTransactions = gameData.GetVendorTransactions(DateTime.MinValue, transactionEndDate);
            foreach (var transaction in vendorTransactions)
            {
                gameData.Remove(transaction);
            }

            var marketTransactions = gameData.GetMarketItemTransactions(DateTime.MinValue, transactionEndDate);
            foreach (var transaction in marketTransactions)
            {
                gameData.Remove(transaction);
            }
        }
    }
}
