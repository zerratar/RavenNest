using Microsoft.EntityFrameworkCore;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RavenNest.DataModels;

namespace RavenNest.BusinessLogic.Game
{
    public interface IServerManager
    {
        Task BroadcastMessageAsync(string message);
    }

    public class ServerManager : IServerManager
    {
        private readonly IGameData gameData;
        public ServerManager(IGameData gameData)
        {
            this.gameData = gameData;
        }

        public Task BroadcastMessageAsync(string message)
        {
            // 1. get all active sessions
            var sessions = gameData.GetActiveSessions();

            //var sessions = await db.GameSession
            //    .Include(x => x.GameEvents)
            //    .Where(x => x.Stopped != null)
            //    .ToListAsync();

            // 2. push a new event for each session
            foreach (var session in sessions)
            {
                //var revision = session.GameEvents.Count > 0
                //    ? session.GameEvents.Max(x => x.Revision) + 1 : 1;

                var gameEvent = gameData.CreateSessionEvent(GameEventType.ServerMessage, session, new ServerMessage()
                {
                    Message = message,
                });

                gameData.Add(gameEvent);

                //await db.GameEvent.AddAsync(new DataModels.GameEvent()
                //{
                //    Id = Guid.NewGuid(),
                //    GameSessionId = session.Id,
                //    GameSession = session,
                //    Data = JSON.Stringify(new ServerMessage()
                //    {
                //        Message = message,
                //    }),
                //    Type = (int)GameEventType.ServerMessage,
                //    Revision = revision
                //});
            }

            return Task.CompletedTask;
        }
    }
}