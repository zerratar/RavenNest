using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Patreon;
using RavenNest.DataModels;
using System;
using System.Linq;

namespace RavenNest.BusinessLogic.Game
{
    public class PatreonManager : IPatreonManager
    {
        private readonly IGameData gameData;

        public PatreonManager(IGameData gameData)
        {
            this.gameData = gameData;
        }

        public void AddPledge(IPatreonData data)
        {
            var user = GetUser(data, out var patreon, true);
            if (user != null)
                user.PatreonTier = patreon.Tier;
        }

        public void RemovePledge(IPatreonData data)
        {
            var user = GetUser(data, out var patreon);
            if (user != null)
                user.PatreonTier = null;
        }

        public void UpdatePledge(IPatreonData data)
        {
            var user = GetUser(data, out var patreon, true);
            if (user != null)
                user.PatreonTier = patreon.Tier;
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

            if (createPatreonIfNotExists && patreon == null)
            {
                patreon = new UserPatreon()
                {
                    Id = Guid.NewGuid(),
                    Email = data.Email,
                    FirstName = firstName,
                    FullName = data.FullName,
                    PatreonId = data.PatreonId,
                    PledgeAmount = data.PledgeAmountCents,
                    PledgeTitle = data.RewardTitle,
                    Tier = data.Tier,
                    TwitchUserId = data.TwitchUserId ?? user?.UserId,
                    UserId = user?.Id
                };
                gameData.Add(patreon);
            }

            return user;
        }

        private User TryGetUser(IPatreonData data)
        {
            var firstName = data.FullName?.Split(' ')?.FirstOrDefault();
            return gameData.FindUser(u =>
            {
                if (u == null)
                    return false;

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
