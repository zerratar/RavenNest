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
            var user = GetUser(data, out var patreon);
            var currentPledgeAmount = patreon.PledgeAmount.GetValueOrDefault();

            var newPledgeAmount = GetPledgeAmount(data);
            if (data.Tier >= patreon.Tier || newPledgeAmount >= currentPledgeAmount)
            {
                patreon.PledgeAmount = newPledgeAmount;
                patreon.PledgeTitle = GetTierTitle(data);
                patreon.Tier = data.Tier;
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
            var firstName = data.FullName?.Split(' ')?.FirstOrDefault();

            var pledgeAmount = GetPledgeAmount(data);
            var title = GetTierTitle(data);

            patreon = new UserPatreon()
            {
                Id = Guid.NewGuid(),
                Email = data.Email,
                FullName = data.FullName,
                PatreonId = data.PatreonId,
                PledgeAmount = pledgeAmount,
                PledgeTitle = title,
                Tier = data.Tier,
                TwitchUserId = data.TwitchUserId,
                FirstName = firstName,
                //TwitchUserId = data.TwitchUserId ?? user?.UserId,                
                //UserId = user?.Id,
                Updated = now,
                Created = now,
            };
            gameData.Add(patreon);
            return patreon;
        }

        private static string GetTierTitle(IPatreonData data)
        {
            var title = data.RewardTitle;
            if (!string.IsNullOrEmpty(title) && title.Contains(','))
            {
                title = title.Split(',')[0];
            }

            return title;
        }

        private static long GetPledgeAmount(IPatreonData data)
        {
            long pledgeAmount = 0;
            if (!string.IsNullOrEmpty(data.PledgeAmountCents))
            {
                var value = data.PledgeAmountCents;
                if (data.PledgeAmountCents.Contains(','))
                {
                    value = data.PledgeAmountCents.Split(',')[0];
                }

                long.TryParse(value, out pledgeAmount);
            }

            return pledgeAmount;
        }

        private User GetUser(IPatreonData data, out UserPatreon patreon)
        {
            patreon = GetOrCreateUserPatreon(data);
            var user = patreon.UserId == null ? TryGetUser(data) : gameData.GetUser(patreon.UserId.GetValueOrDefault());
            var now = DateTime.UtcNow;

            if (patreon.UserId == null)
            {
                if (!string.IsNullOrEmpty(data.TwitchUserId))
                {
                    patreon.TwitchUserId = data.TwitchUserId;
                    patreon.Updated = now;
                }

                if (user != null)
                {
                    patreon.TwitchUserId = user.UserId;
                    patreon.UserId = user.Id;
                    patreon.Updated = now;
                }
            }

            if (string.IsNullOrEmpty(patreon.FirstName))
            {
                patreon.FirstName = data.FullName?.Split(' ')?.FirstOrDefault();
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
            var emailLower = data.Email.ToLower();
            var emailuser = emailLower.Split('@').FirstOrDefault();
            return gameData.FindUser(u =>
            {
                if (u == null)
                    return false;

                var email = u.Email?.ToLower() ?? string.Empty;

                if (!string.IsNullOrEmpty(twitchUserName) && u.UserName.ToLower() == twitchUserName)
                    return true;

                if (!string.IsNullOrEmpty(data.TwitchUserId) && u.UserId == data.TwitchUserId)
                    return true;

                if (!string.IsNullOrEmpty(u.UserName) && (u.UserName.ToLower() == firstName?.ToLower() || u.UserName.ToLower() == emailuser))
                    return true;

                if (email == emailLower || email.StartsWith(emailuser + "@"))
                    return true;

                return false;
            });
        }
    }
}
