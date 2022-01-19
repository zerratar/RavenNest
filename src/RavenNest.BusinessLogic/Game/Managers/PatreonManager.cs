using Microsoft.Extensions.Logging;
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
            UpdatePledge(data);
        }

        public void RemovePledge(IPatreonData data)
        {
            var user = GetUser(data, out var patreon);

            return; // don't remove anything, but we should flag it to expire?

            //if (user != null &&
            //    (data.Status == null || data.Status.IndexOf("active", StringComparison.OrdinalIgnoreCase) < 0))
            //    user.PatreonTier = null;

            //user.PatreonExpires = ...
        }

        public void UpdatePledge(IPatreonData data)
        {
            var patreon = GetOrCreateUserPatreon(data);
            var user = patreon.UserId != null ? gameData.GetUser(patreon.UserId.Value) : TryGetUser(data);

            if (patreon.UserId == null && user != null)
            {
                patreon.UserId = user.Id;
                patreon.TwitchUserId = user.UserId;
            }

            var currentPledgeAmount = patreon.PledgeAmount.GetValueOrDefault();

            if (long.TryParse(data.PledgeAmountCents ?? "0", out var newPledgeAmount) && newPledgeAmount > currentPledgeAmount)
            {
                patreon.PledgeAmount = newPledgeAmount;
                patreon.Tier = data.Tier;
                patreon.PledgeTitle = data.RewardTitle;
            }

            if (user != null && patreon.Tier > user.PatreonTier)
            {
                user.PatreonTier = patreon.Tier;
            }

            patreon.Updated = DateTime.UtcNow;
        }



        private UserPatreon GetOrCreateUserPatreon(IPatreonData data)
        {
            var patreon = gameData.GetPatreonUser(data.PatreonId);
            if (patreon != null)
            {
                return patreon;
            }

            var now = DateTime.UtcNow;
            long.TryParse(data.PledgeAmountCents ?? "0", out var pledgeAmount);

            patreon = new UserPatreon()
            {
                Id = Guid.NewGuid(),
                Email = data.Email,
                FullName = data.FullName,
                PatreonId = data.PatreonId,
                PledgeAmount = pledgeAmount,
                PledgeTitle = data.RewardTitle,
                Tier = data.Tier,
                TwitchUserId = data.TwitchUserId,
                //TwitchUserId = data.TwitchUserId ?? user?.UserId,                
                //UserId = user?.Id,
                Updated = now,
                Created = now,
            };
            gameData.Add(patreon);
            return patreon;
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

            var emailuser = data.Email.ToLower().Split('@').FirstOrDefault();
            return gameData.FindUser(u =>
            {
                if (u == null)
                    return false;

                if (!string.IsNullOrEmpty(twitchUserName) && u.UserName.ToLower() == twitchUserName)
                    return true;

                if (!string.IsNullOrEmpty(data.TwitchUserId) && u.UserId == data.TwitchUserId)
                    return true;

                if (!string.IsNullOrEmpty(u.UserName) && (u.UserName.ToLower() == firstName?.ToLower() || u.UserName.ToLower() == emailuser))
                    return true;

                if (u.Email?.ToLower() == data.Email.ToLower())
                    return true;

                return false;
            });
        }
    }
}
