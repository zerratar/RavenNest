using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Extensions;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    public class GameManager : IGameManager
    {
        private readonly IRavenfallDbContextProvider dbProvider;

        public GameManager(IRavenfallDbContextProvider dbProvider)
        {
            this.dbProvider = dbProvider;
        }

        public Task<GameInfo> GetGameInfoAsync(SessionToken session)
        {
            return null;
        }

        public async Task<EventCollection> GetGameEventsAsync(
            SessionToken session)
        {
            using (var db = dbProvider.Get())
            {
                var gameSession = await db.GameSession.FirstOrDefaultAsync(x => x.Id == session.SessionId);
                if (gameSession == null)
                {
                    return new EventCollection();
                }

                var events = await db.GameEvent.Where(x =>
                    x.GameSessionId == session.SessionId &&
                    x.Revision > gameSession.Revision).ToListAsync();

                var eventCollection = new EventCollection();

                foreach (var ev in events)
                {
                    var gameEvent = ModelMapper.Map(ev);
                    if (eventCollection.Revision < gameEvent.Revision)
                        eventCollection.Revision = gameEvent.Revision;
                    eventCollection.Add(gameEvent);
                }

                var oldRev = gameSession.Revision;
                if (eventCollection.Revision > gameSession.Revision)
                {
                    gameSession.Revision = eventCollection.Revision;
                    db.Update(gameSession);
                    await db.SaveChangesAsync();
                }

                return eventCollection;
            }
        }
    }
}