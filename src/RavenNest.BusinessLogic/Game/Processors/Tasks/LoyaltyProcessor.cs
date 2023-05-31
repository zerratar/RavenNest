using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Providers;
using RavenNest.DataModels;
using System;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public class LoyaltyProcessor : PlayerTaskProcessor
    {
        private static readonly TimeSpan ActivityTimeout = TimeSpan.FromMinutes(5);

        private readonly ConcurrentDictionary<Guid, DateTime> lastUpdate
            = new ConcurrentDictionary<Guid, DateTime>();

        private readonly ConcurrentDictionary<Guid, DateTime> activity
            = new ConcurrentDictionary<Guid, DateTime>();

        private readonly ConcurrentDictionary<Guid, CharacterState> previousState
            = new ConcurrentDictionary<Guid, CharacterState>();

        public override void Process(
            GameData gameData,
            PlayerInventoryProvider inventoryProvider,
            GameSession session,
            Character character,
            CharacterState state)
        {
            var user = gameData.GetUser(character.UserId);

            var loyalty = gameData.GetUserLoyalty(character.UserId, session.UserId);
            if (loyalty == null && character.UserIdLock != null)
            {
                loyalty = gameData.GetUserLoyalty(character.UserId, character.UserIdLock.GetValueOrDefault());
            }

            if (loyalty == null)
            {
                loyalty = CreateUserLoyalty(gameData, session, user);
            }

            //bool isActive = CheckUserActivity(user.Id, character, state);

            DateTime now = DateTime.UtcNow;
            TimeSpan elapsed = TimeSpan.Zero;
            if (lastUpdate.TryGetValue(user.Id, out var lastUpdateTime))
            {
                elapsed = now - lastUpdateTime;
                loyalty.AddPlayTime(elapsed);
            }
            lastUpdate[user.Id] = now;

            var isSubMdVip = loyalty.IsSubscriber;
            var activityMultiplier = isSubMdVip ? UserLoyalty.SubscriberMultiplier : 1;
            loyalty.AddExperience((double)elapsed.TotalSeconds * UserLoyalty.ExpPerSecond * activityMultiplier);
        }

        private UserLoyalty CreateUserLoyalty(
            GameData gameData,
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
