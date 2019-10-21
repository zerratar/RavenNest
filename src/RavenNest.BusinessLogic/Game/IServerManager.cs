using Microsoft.EntityFrameworkCore;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Net;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace RavenNest.BusinessLogic.Game
{
    public interface IServerManager
    {
        Task BroadcastMessageAsync(string message);
    }

    public class ServerManager : IServerManager
    {
        private readonly IRavenfallDbContextProvider dbProvider;
        public ServerManager(IRavenfallDbContextProvider dbProvider)
        {
            this.dbProvider = dbProvider;
        }

        public async Task BroadcastMessageAsync(string message)
        {
            using (var db = dbProvider.Get())
            {
                // 1. get all active sessions
                var sessions = await db.GameSession
                    .Include(x => x.GameEvents)
                    .Where(x => x.Stopped != null)
                    .ToListAsync();

                // 2. push a new event for each session
                foreach (var session in sessions)
                {
                    var revision = session.GameEvents.Count > 0
                        ? session.GameEvents.Max(x => x.Revision) + 1 : 1;

                    await db.GameEvent.AddAsync(new DataModels.GameEvent()
                    {
                        Id = Guid.NewGuid(),
                        GameSessionId = session.Id,
                        GameSession = session,
                        Data = JSON.Stringify(new ServerMessage()
                        {
                            Message = message,
                        }),
                        Type = (int)GameEventType.ServerMessage,
                        Revision = revision
                    });
                }

                await db.SaveChangesAsync();
            }
        }
    }
}