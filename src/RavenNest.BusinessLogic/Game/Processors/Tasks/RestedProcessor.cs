﻿using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Providers;
using RavenNest.DataModels;
using System;
using System.Collections.Concurrent;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public class RestedProcessor : PlayerTaskProcessor
    {
        private readonly ConcurrentDictionary<Guid, DateTime> lastUpdate
            = new ConcurrentDictionary<Guid, DateTime>();

        private readonly ConcurrentDictionary<Guid, DateTime> lastEvent
            = new ConcurrentDictionary<Guid, DateTime>();

        private readonly TimeSpan RestedEventUpdateInterval = TimeSpan.FromSeconds(1);
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
                && string.IsNullOrEmpty(state.DuelOpponent);

            var now = DateTime.UtcNow;
            if(!lastUpdate.TryGetValue(character.Id, out var lastUpdateTime))
            {
                lastUpdateTime = now;
            }

            var elapsed = now - lastUpdateTime;
            if (isResting)
            {
                AddRestTime(state, elapsed);
            }
            else
            {
                RemoveRestTime(state, elapsed);
            }

            if (!lastEvent.TryGetValue(character.Id, out var lastEventUpdate))
            {
                lastEventUpdate = DateTime.MinValue;
            }

            var sinceLastEvent = now - lastEventUpdate;
            if (sinceLastEvent >= RestedEventUpdateInterval)
            {
                var restedTime = (double)(state.RestedTime ?? 0);
                var restedPercent = restedTime / MaxRestTime.TotalSeconds;
                var isRested = restedTime > 0;

                var gameEvent = gameData.CreateSessionEvent(
                    GameEventType.PlayerRestedUpdate,
                    session,
                    new Models.PlayerRestedUpdate
                    {
                        CharacterId = character.Id,
                        ExpBoost = isRested ? ExpBoost : 0,
                        StatsBoost = isRested ? CombatStatsBoost : 0,
                        RestedTime = restedTime,
                        RestedPercent = restedPercent
                    }
                );
                gameData.Add(gameEvent);
                lastEvent[character.Id] = now;
            }
            lastUpdate[character.Id] = now;
        }

        private void RemoveRestTime(CharacterState state, TimeSpan elapsed)
        {
            var restedTime = state.RestedTime ?? 0m;
            state.RestedTime = Math.Max(0, restedTime - (decimal)(elapsed.TotalSeconds * RestedDrainFactor));
        }

        private void AddRestTime(CharacterState state, TimeSpan elapsed)
        {
            var restedTime = state.RestedTime ?? 0m;
            state.RestedTime = Math.Min((decimal)MaxRestTime.TotalSeconds, restedTime + (decimal)(elapsed.TotalSeconds * RestedGainFactor));
        }
    }
}