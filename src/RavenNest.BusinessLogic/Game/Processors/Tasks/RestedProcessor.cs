using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Providers;
using RavenNest.DataModels;
using System;
using System.Collections.Concurrent;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public class PlayerRestedState
    {
        public DateTime Updated;
        public bool Resting;
        public double RestedTime;
    }

    public class RestedProcessor : PlayerTaskProcessor
    {
        private readonly ConcurrentDictionary<Guid, DateTime> lastUpdate
            = new ConcurrentDictionary<Guid, DateTime>();

        private readonly ConcurrentDictionary<Guid, PlayerRestedState> lastEvent
            = new ConcurrentDictionary<Guid, PlayerRestedState>();

        private readonly TimeSpan RestedEventUpdateInterval = TimeSpan.FromSeconds(5);
        private readonly TimeSpan MaxRestTime = TimeSpan.FromHours(2);

        private const double RestedGainFactor = 2.0;
        private const double RestedDrainFactor = 1.0;
        private const double ExpBoost = 2;
        private const double CombatStatsBoost = 0.15;

        public override void Process(
            IIntegrityChecker integrityChecker,
            IGameData gameData,
            IPlayerInventoryProvider inventoryProvider,
            GameSession session,
            Character character,
            CharacterState state)
        {
            if (state == null)
                return;

            var isResting = state.InOnsen.GetValueOrDefault()
                && !state.InDungeon.GetValueOrDefault()
                && !state.InArena
                && !state.InRaid
                && !string.IsNullOrEmpty(state.Island)
                && string.IsNullOrEmpty(state.DuelOpponent);

            var now = DateTime.UtcNow;
            if (!lastUpdate.TryGetValue(character.Id, out var lastUpdateTime))
            {
                lastUpdate[character.Id] = (lastUpdateTime = now);
            }

            var elapsed = now - lastUpdateTime;
            var requireUpdate = isResting
                ? AddRestTime(state, elapsed)
                : RemoveRestTime(state, elapsed);

            if (!lastEvent.TryGetValue(character.Id, out var lastEventUpdate))
            {
                lastEvent[character.Id] = (lastEventUpdate = new PlayerRestedState());
            }

            if (requireUpdate)
            {
                var sinceLastEvent = now - lastEventUpdate.Updated;
                var restedTime = (double)(state.RestedTime ?? 0);
                var isRested = restedTime > 0;

                if (sinceLastEvent >= RestedEventUpdateInterval && (lastEventUpdate.RestedTime != restedTime || lastEventUpdate.Resting != isResting))
                {
                    var restedPercent = restedTime / MaxRestTime.TotalSeconds;

                    var data = new Models.PlayerRestedUpdate
                    {
                        CharacterId = character.Id,
                        ExpBoost = isRested ? ExpBoost : 0,
                        StatsBoost = isRested ? CombatStatsBoost : 0,
                        RestedTime = restedTime,
                        RestedPercent = restedPercent,
                    };

                    if (TcpConnectionProvider.TryGet(session.Id, out var connection))
                    {
                        connection.Send(data);
                    }
                    else
                    {
                        var gameEvent = gameData.CreateSessionEvent(GameEventType.PlayerRestedUpdate, session, data);
                        gameData.Add(gameEvent);
                    }

                    lastEventUpdate.RestedTime = restedTime;
                    lastEventUpdate.Resting = isResting;
                    lastEventUpdate.Updated = now;

                    if (restedTime > 0)
                    {
                        TrySendToExtensionAsync(character, data);
                    }
                }
            }

            lastUpdate[character.Id] = now;
        }


        private bool RemoveRestTime(CharacterState state, TimeSpan elapsed)
        {
            var before = state.RestedTime;
            var restedTime = state.RestedTime ?? 0d;
            state.RestedTime = Math.Max(0, restedTime - (double)(elapsed.TotalSeconds * RestedDrainFactor));
            return before > state.RestedTime;
        }

        private bool AddRestTime(CharacterState state, TimeSpan elapsed)
        {
            var restedTime = state.RestedTime ?? 0d;
            var before = state.RestedTime;
            state.RestedTime = Math.Min((double)MaxRestTime.TotalSeconds, restedTime + (double)(elapsed.TotalSeconds * RestedGainFactor));
            return state.RestedTime > before;
        }
    }
}
