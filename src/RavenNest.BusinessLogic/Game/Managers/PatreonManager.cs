using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Patreon;
using RavenNest.DataModels;
using System;

namespace RavenNest.BusinessLogic.Game
{
    public class PatreonManager : IPatreonManager
    {
        private readonly IGameData gameData;

        public PatreonManager(IGameData gameData)
        {
            this.gameData = gameData;
        }

        public void AddPledge(PatreonPledgeData data)
        {
            var user = GetUser(data, out var patreon, true);
            if (user != null)
                user.PatreonTier = patreon.Tier;
        }

        public void RemovePledge(PatreonPledgeData data)
        {
            var user = GetUser(data, out var patreon);
            if (user != null)
                user.PatreonTier = null;
        }

        public void UpdatePledge(PatreonPledgeData data)
        {
            var user = GetUser(data, out var patreon, true);
            if (user != null)
                user.PatreonTier = patreon.Tier;
        }

        private User GetUser(PatreonPledgeData data, out UserPatreon patreon, bool createPatreonIfNotExists = false)
        {
            User user = null;
            patreon = gameData.GetPatreonUser(data.PatreonId);
            if (patreon == null || patreon.UserId == null)
                user = TryGetUser(data);
            else
                user = gameData.GetUser(patreon.UserId.GetValueOrDefault());

            if (createPatreonIfNotExists)
            {
                patreon = new UserPatreon()
                {
                    Id = Guid.NewGuid(),
                    Email = data.Email,
                    FirstName = data.FirstName,
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

        private User TryGetUser(PatreonPledgeData data)
        {
            return gameData.FindUser(u =>
            {
                if (u == null)
                    return false;

                if (u.UserId == data.TwitchUserId)
                    return true;

                if (!string.IsNullOrEmpty(u.UserName) && u.UserName.ToLower() == data.FirstName?.ToLower())
                    return true;

                if (u.Email?.ToLower() == data.Email.ToLower())
                    return true;

                return false;
            });
        }
    }
}
