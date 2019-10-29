using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Extensions;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    public class GameManager : IGameManager
    {
        private readonly IGameData gameData;

        public GameManager(IGameData gameData)
        {
            this.gameData = gameData;
        }

        public GameInfo GetGameInfo(SessionToken session)
        {
            return null;
        }

        public EventCollection GetGameEvents(SessionToken session)
        {
            var gameSession = gameData.GetSession(session.SessionId);
            if (gameSession == null)
            {
                return new EventCollection();
            }

            //var sessionRevision = gameSession.Revision ?? 0;
            //var events = await db.GameEvent.Where(x =>
            //    x.GameSessionId == session.SessionId &&
            //    x.Revision > sessionRevision).ToListAsync();

            var events = gameData.GetSessionEvents(gameSession);            
            var eventCollection = new EventCollection();

            foreach (var ev in events)
            {
                var gameEvent = ModelMapper.Map(ev);
                if (eventCollection.Revision < gameEvent.Revision)
                    eventCollection.Revision = gameEvent.Revision;
                eventCollection.Add(gameEvent);
            }

            if (eventCollection.Revision > gameSession.Revision)
            {
                gameSession.Revision = eventCollection.Revision;
                gameData.Update(gameSession);
            }

            return eventCollection;
        }
    }
}