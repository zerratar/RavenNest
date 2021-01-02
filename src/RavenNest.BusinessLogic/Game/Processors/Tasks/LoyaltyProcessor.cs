using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Providers;
using RavenNest.DataModels;
using System;
using System.Collections.Concurrent;

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

        public override void Handle(
            IIntegrityChecker integrityChecker,
            IGameData gameData,
            IPlayerInventoryProvider inventoryProvider,
            GameSession session,
            Character character,
            CharacterState state)
        {
            var user = gameData.GetUser(character.UserId);

            var loyalty = gameData.GetUserLoyalty(character.UserId, session.UserId);
            if (loyalty == null)
                loyalty = CreateUserLoyalty(gameData, session, user);

            bool isActive = CheckUserActivity(user.Id, character, state);

            DateTime now = DateTime.UtcNow;
            TimeSpan elapsed = TimeSpan.Zero;
            if (lastUpdate.TryGetValue(user.Id, out var lastUpdateTime))
            {
                elapsed = now - lastUpdateTime;
                loyalty.AddPlayTime(elapsed);
            }
            lastUpdate[user.Id] = now;

            var isSubMdVip = loyalty.IsSubscriber || loyalty.IsModerator || loyalty.IsVip;
            var activityMultiplier = isSubMdVip ? UserLoyalty.ActivityMultiplier : 1m;
            loyalty.AddExperience((decimal)elapsed.TotalSeconds * UserLoyalty.ExpPerSecond * activityMultiplier);
        }

        private bool CheckUserActivity(Guid userId, Character character, CharacterState state)
        {
            if (DateTime.UtcNow - character.LastUsed < ActivityTimeout)
            {
                activity[userId] = character.LastUsed.GetValueOrDefault();
                previousState[userId] = CopyState(state);
                return true;
            }

            if (previousState.TryGetValue(userId, out var prevState) && StateHasChanged(state, prevState))
            {
                activity[userId] = DateTime.UtcNow;
                previousState[userId] = CopyState(state);
                return true;
            }

            if (activity.TryGetValue(userId, out var lastActivity) && DateTime.UtcNow - lastActivity < ActivityTimeout)
            {
                return true;
            }

            return false;
        }

        private bool StateHasChanged(CharacterState state, CharacterState prevState)
        {
            return state.DuelOpponent != prevState.DuelOpponent ||
                state.Task != prevState.Task ||
                state.TaskArgument != prevState.TaskArgument ||
                state.Island != prevState.Island ||
                state.InRaid != prevState.InRaid ||
                state.InDungeon != prevState.InDungeon ||
                state.InArena != prevState.InArena;
        }

        private CharacterState CopyState(CharacterState state)
        {
            return new CharacterState
            {
                Id = state.Id,
                DuelOpponent = state.DuelOpponent,
                Health = state.Health,
                InArena = state.InArena,
                InDungeon = state.InDungeon,
                InRaid = state.InRaid,
                Island = state.Island,
                Task = state.Task,
                TaskArgument = state.TaskArgument,
                X = state.X,
                Y = state.Y,
                Z = state.Z
            };
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
