using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Patreon;
using RavenNest.BusinessLogic.Providers;
using RavenNest.DataModels;
using System;
using System.Linq;

namespace RavenNest.BusinessLogic.Game
{
    public class PatreonManager : IPatreonManager
    {
        private readonly IGameData gameData;
        private readonly IPlayerInventoryProvider playerInventory;

        public PatreonManager(
            IGameData gameData,
            IPlayerInventoryProvider playerInventory)
        {
            this.gameData = gameData;
            this.playerInventory = playerInventory;
        }

        public void AddPledge(IPatreonData data)
        {
            var user = GetUser(data, out var patreon, true);
            if (user != null)
            {
                //var main = gameData.GetCharacterByUserId(user.Id);
                //if (main != null)
                //{
                //    var inventory = playerInventory.Get(main.Id);
                //    inventory.AddPatreonTierRewards(data.Tier);
                //}

                var newTier = patreon.Tier.GetValueOrDefault();
                var oldTier = user.PatreonTier.GetValueOrDefault();

                if (newTier > oldTier) // only allow automatic increase of tiers. 
                    user.PatreonTier = newTier;
            }
        }

        public void RemovePledge(IPatreonData data)
        {
            var user = GetUser(data, out var patreon);

            return; // don't remove anything, but we should flag it to expire?

            if (user != null &&
                (data.Status == null || data.Status.IndexOf("active", StringComparison.OrdinalIgnoreCase) < 0))
                user.PatreonTier = null;

            //user.PatreonExpires = ...
        }

        public void UpdatePledge(IPatreonData data)
        {
            var user = GetUser(data, out var patreon, true);
            if (user != null)
            {
                //var main = gameData.GetCharacterByUserId(user.Id);
                //if (main != null)
                //{
                //    var inventory = playerInventory.Get(main.Id);
                //    inventory.AddPatreonTierRewards(data.Tier);
                //}
                var newTier = patreon.Tier.GetValueOrDefault();
                var oldTier = user.PatreonTier.GetValueOrDefault();

                if (newTier > oldTier) // only allow automatic increase of tiers. 
                    user.PatreonTier = newTier;
            }
        }

        private User GetUser(IPatreonData data, out UserPatreon patreon, bool createPatreonIfNotExists = false)
        {
            User user = null;
            var firstName = data.FullName?.Split(' ')?.FirstOrDefault();
            patreon = gameData.GetPatreonUser(data.PatreonId);
            if (patreon == null || patreon.UserId == null)
                user = TryGetUser(data);
            else
                user = gameData.GetUser(patreon.UserId.GetValueOrDefault());

            var now = DateTime.UtcNow;
            if (createPatreonIfNotExists && patreon == null)
            {
                long pledgeAmount = 0;
                if (!string.IsNullOrEmpty(data.PledgeAmountCents))
                {
                    var value = data.PledgeAmountCents;
                    if (data.PledgeAmountCents.Contains(','))
                    {
                        value = data.PledgeAmountCents.Split(',')[1];
                    }

                    long.TryParse(value, out pledgeAmount);
                }

                string title = data.RewardTitle;
                if (!string.IsNullOrEmpty(title) && title.Contains(','))
                {
                    title = title.Split(',')[1];
                }

                patreon = new UserPatreon()
                {
                    Id = Guid.NewGuid(),
                    Email = data.Email,
                    FirstName = firstName,
                    FullName = data.FullName,
                    PatreonId = data.PatreonId,
                    PledgeAmount = pledgeAmount,
                    PledgeTitle = title,
                    Tier = data.Tier,
                    TwitchUserId = data.TwitchUserId ?? user?.UserId,
                    UserId = user?.Id,
                    Updated = now,
                    Created = now,
                };
                gameData.Add(patreon);
            }
            else if (patreon != null)
            {
                patreon.Updated = now;
            }

            return user;
        }

        private User TryGetUser(IPatreonData data)
        {
            var firstName = data.FullName?.Split(' ')?.FirstOrDefault();
            var twitchUserName = "";
            if (!string.IsNullOrEmpty(data.TwitchUrl))
            {
                twitchUserName = data.TwitchUrl.Split('/').LastOrDefault()?.ToLower();
            }

            return gameData.FindUser(u =>
            {
                if (u == null)
                    return false;

                if (!string.IsNullOrEmpty(twitchUserName) && u.UserName.ToLower() == twitchUserName)
                    return true;

                if (!string.IsNullOrEmpty(data.TwitchUserId) && u.UserId == data.TwitchUserId)
                    return true;

                if (!string.IsNullOrEmpty(u.UserName) && u.UserName.ToLower() == firstName?.ToLower())
                    return true;

                if (u.Email?.ToLower() == data.Email.ToLower())
                    return true;

                return false;
            });
        }
    }
}
