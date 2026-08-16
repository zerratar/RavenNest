using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Net;
using System;
using RavenNest.DataModels;
using System.Collections.Generic;

namespace RavenNest.BusinessLogic.Game
{
    public class ServerManager : IServerManager
    {
        private readonly GameData gameData;
        public ServerManager(GameData gameData)
        {
            this.gameData = gameData;
        }

        public void BroadcastMessageAsync(string message, int time)
        {
            var sessions = gameData.GetActiveSessions();

            foreach (var session in sessions)
            {
                var gameEvent = gameData.CreateSessionEvent(RavenNest.Models.GameEventType.ServerMessage, session, new ServerMessage()
                {
                    Message = message,
                    Time = time
                });

                gameData.EnqueueGameEvent(gameEvent);
            }
        }

        public bool CanIncreaseGlobalExpMultiplier()
        {
            return CanIncreaseGlobalExpMultiplier(1);
        }

        public bool CanIncreaseGlobalExpMultiplier(int count)
        {
            var activeEvent = gameData.GetActiveExpMultiplierEvent();
            if (activeEvent != null && !activeEvent.StartedByPlayer)
            {
                return false;
            }

            if (activeEvent != null && (activeEvent.Multiplier + count) > SessionManager.MaxPlayerExpMultiplier)
            {
                return false;
            }

            return true;
        }

        public int GetIncreasableGlobalExpAmount()
        {
            var activeEvent = gameData.GetActiveExpMultiplierEvent();
            if (activeEvent == null)
            {
                return SessionManager.MaxPlayerExpMultiplier - 1;
            }

            if ((activeEvent != null && !activeEvent.StartedByPlayer))
            {
                return 0;
            }

            return SessionManager.MaxPlayerExpMultiplier - activeEvent.Multiplier;
        }

        public bool IncreaseGlobalExpMultiplier(DataModels.User user)
        {
            return IncreaseGlobalExpMultiplier(user, 1);
        }

        public bool IncreaseGlobalExpMultiplier(User user, int usageCount)
        {
            var activeEvent = gameData.GetActiveExpMultiplierEvent();
            if (activeEvent != null && !activeEvent.StartedByPlayer) return false;
            if (activeEvent == null)
            {
                var endTime = DateTime.UtcNow.AddMinutes(SessionManager.ExpMultiplierStartTimeMinutes);

                if (usageCount > 1)
                {
                    endTime = usageCount >= SessionManager.MaxPlayerExpMultiplier - 1
                        ? endTime.AddMinutes(SessionManager.ExpMultiplierLastTimeMinutes + ((SessionManager.MaxPlayerExpMultiplier - 2) * SessionManager.ExpMultiplierMinutesPerScroll))
                        : endTime.AddMinutes(usageCount * SessionManager.ExpMultiplierMinutesPerScroll);
                }

                activeEvent = new ExpMultiplierEvent
                {
                    Id = Guid.NewGuid(),
                    Multiplier = usageCount + 1,
                    StartedByPlayer = true,
                    EventName = user.UserName,
                    StartTime = DateTime.UtcNow,
                    EndTime = endTime
                };
                gameData.Add(activeEvent);
            }
            else
            {
                if (activeEvent.Multiplier >= SessionManager.MaxPlayerExpMultiplier)
                {
                    return false;
                }

                var endTime = activeEvent.EndTime;
                activeEvent.Multiplier += usageCount;

                var timeCount = usageCount;
                if (activeEvent.Multiplier >= SessionManager.MaxPlayerExpMultiplier)
                {
                    endTime = endTime.AddMinutes(SessionManager.ExpMultiplierLastTimeMinutes);
                    timeCount--;
                }

                if (timeCount > 0)
                {
                    endTime = endTime.AddMinutes(timeCount * SessionManager.ExpMultiplierMinutesPerScroll);
                }

                activeEvent.EndTime = endTime;
            }

            activeEvent.EventName = user.UserName;
            return true;
        }

        public ExpMultiplierEvent SendExpMultiplierEventAsync(int multiplier, string message, DateTime? startTime, DateTime endTime)
        {
            var start = startTime ?? DateTime.UtcNow;
            var activeEvent = gameData.GetActiveExpMultiplierEvent();

            if (activeEvent != null)
            {
                // if the start time 
                if (startTime >= activeEvent.EndTime)
                {
                    return AddExpMultiplierr(multiplier, message, start, endTime);
                }

                if ((start >= activeEvent.StartTime && start <= activeEvent.EndTime) || (endTime >= activeEvent.EndTime && startTime <= activeEvent.StartTime))
                {
                    if (activeEvent.StartedByPlayer)
                    {
                        // when we overriding the multiplier that exists placed by a player
                        // we must be able to go back to their multiplier as soon as our server multi is over.
                        // we will update the current event, push it forward until after the overriden multi is over
                        // but reduce the length with the past time. Since this is active currently. UtcNow has already past its start time.

                        var elapsedTime = DateTime.UtcNow - activeEvent.StartTime;
                        var duration = activeEvent.EndTime - activeEvent.StartTime;
                        var remaining = duration.Subtract(elapsedTime);

                        activeEvent.StartTime = endTime;
                        activeEvent.EndTime = endTime.Add(remaining);

                        // finally, add the new multiplier

                        return AddExpMultiplierr(multiplier, message, start, endTime);
                    }

                    // otherwise we will update the current multiplier with the new values
                    activeEvent.Multiplier = multiplier;
                    activeEvent.StartTime = start;
                    activeEvent.EndTime = endTime;
                    activeEvent.EventName = message;
                    return activeEvent;
                }
            }

            return AddExpMultiplierr(multiplier, message, start, endTime);
        }

        private ExpMultiplierEvent AddExpMultiplierr(int multiplier, string eventName, DateTime startTime, DateTime endTime, bool startedByPlayer = false)
        {
            if (endTime < startTime)
            {
                var tmp = endTime;
                endTime = startTime;
                startTime = tmp;
            }

            var ev = new ExpMultiplierEvent();
            ev.Id = Guid.NewGuid();
            ev.EventName = eventName;
            ev.StartedByPlayer = startedByPlayer;
            ev.Multiplier = multiplier;
            ev.StartTime = startTime;
            ev.EndTime = endTime;
            gameData.Add(ev);
            return ev;
        }

        /// <summary>
        ///     Stores what the bot last reported about itself.
        /// </summary>
        /// <remarks>
        ///     Both shapes are filled, and that is the fix rather than a tidy-up. This used to
        ///     deserialise into the dictionary and fall back to the typed BotStats only if that
        ///     threw. Deserialising a JSON object into Dictionary&lt;string, object&gt; does not
        ///     throw, so the fallback never ran and no typed property was ever populated:
        ///     JoinedChannelsCount stayed 0 and ListOfCurrentlyJoinedChannel stayed empty however
        ///     many channels the bot was actually in.
        ///
        ///     <para>
        ///     Only LastUpdated escaped it, because it is assigned below rather than deserialised,
        ///     which is why the online check worked while everything else read as zero. The
        ///     visible symptom was BotStats.IsInChannel always answering false, so the /bot page
        ///     told every streamer the bot was not in their channel.
        ///     </para>
        ///
        ///     <para>
        ///     Errors are handled per member rather than per document. This is defence rather than
        ///     necessity: checked against what ROBot actually serialises, a strict pass succeeds,
        ///     because the bot sends real TimeSpan and DateTime values and Newtonsoft writes them
        ///     in a form it can read back. The two models do drift apart though, the bot counting
        ///     in ulong where this counts in int and long, so one field that will not convert
        ///     should cost that field rather than the whole report.
        ///     </para>
        /// </remarks>
        public void UpdateBotStats(string data)
        {
            if (string.IsNullOrEmpty(data)) return;

            var lenient = new Newtonsoft.Json.JsonSerializerSettings
            {
                Error = (_, args) => args.ErrorContext.Handled = true
            };

            try
            {
                var stats = Newtonsoft.Json.JsonConvert.DeserializeObject<BotStats>(data, lenient);
                if (stats != null)
                {
                    gameData.Bot = stats;
                }
            }
            catch
            {
                // Keep the previous report rather than blanking it.
            }

            try
            {
                gameData.Bot.Values = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(data);
            }
            catch
            {
                // The typed half above is still usable without the raw dictionary.
            }

            gameData.Bot.LastUpdated = DateTime.UtcNow;
        }
    }
}
