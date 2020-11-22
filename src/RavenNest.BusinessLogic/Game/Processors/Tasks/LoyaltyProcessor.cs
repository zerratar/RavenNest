using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;
using System;
using System.Collections.Concurrent;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public class LoyaltyProcessor : PlayerTaskProcessor
    {
        private readonly ConcurrentDictionary<Guid, DateTime> lastUpdate
            = new ConcurrentDictionary<Guid, DateTime>();

        public override void Handle(
            IIntegrityChecker integrityChecker,
            IGameData gameData,
            IPlayerInventoryProvider inventoryProvider,
            GameSession session,
            Character character,
            CharacterState state)
        {
            var user = gameData.GetUser(character.UserId);
            var loyalty = gameData.GetUserLoyalty(character.UserId);
            if (loyalty == null)
                loyalty = CreateUserLoyalty(gameData, session, user);

            DateTime now = DateTime.UtcNow;
            TimeSpan elapsed = TimeSpan.Zero;
            DateTime lastUpdateTime = now;
            if (lastUpdate.TryGetValue(user.Id, out lastUpdateTime))
            {
                elapsed = now - lastUpdateTime;
                loyalty.AddPlayTime(elapsed);
            }
            lastUpdate[user.Id] = now;

            // only add EXP if they are a subscriber, moderator or vip
            if (!loyalty.IsSubscriber && !loyalty.IsModerator && !loyalty.IsVip && loyalty.UserId != loyalty.StreamerUserId)
                return;

            loyalty.AddExperience((decimal)elapsed.TotalSeconds * UserLoyalty.ExpPerSecond);
        }

        private UserLoyalty CreateUserLoyalty(
            IGameData gameData,
            GameSession session,
            User user)
        {
            var loyalty = new UserLoyalty
            {
                Id = Guid.NewGuid(),
                Playtime = "00:00:00",
                Points = 0,
                Experience = 0,
                StreamerUserId = session.UserId,
                UserId = user.Id,
                Level = 1,
                CheeredBits = 0,
                GiftedSubs = 0
            };
            gameData.Add(loyalty);
            return loyalty;
        }
    }
}
