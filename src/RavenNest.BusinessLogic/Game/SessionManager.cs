using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    public class SessionManager : ISessionManager
    {
        private readonly IRavenfallDbContextProvider dbProvider;

        public SessionManager(IRavenfallDbContextProvider dbProvider)
        {
            this.dbProvider = dbProvider;
        }

        public async Task<SessionToken> BeginSessionAsync(AuthToken token, string clientVersion, string accessKey, bool isLocal)
        {
            var db = dbProvider.Get();
            var game = await db.GameClient.FirstOrDefaultAsync(x => x.ClientVersion == clientVersion);

            if (game == null)
            {
                return null;
            }

            if (game.AccessKey != accessKey)
            {
                return null;
            }

            var userId = token.UserId;
            var activeSession = await db.GameSession
                .FirstOrDefaultAsync(
                    x => x.UserId == userId && x.Status == (int)SessionStatus.Active);

            if (activeSession != null)
            {
                return GenerateSessionToken(token, activeSession);
            }

            var newGameSession = new GameSession
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Status = (int)SessionStatus.Active,
                Started = DateTime.UtcNow,
                Local = isLocal
            };

            await db.GameSession.AddAsync(newGameSession);
            await db.SaveChangesAsync();

            return GenerateSessionToken(token, newGameSession);
        }

        private static SessionToken GenerateSessionToken(AuthToken token, GameSession session)
        {
            return new SessionToken
            {
                AuthToken = token.Token,
                ExpiresUtc = DateTime.UtcNow + TimeSpan.FromHours(12),
                SessionId = session.Id,
                StartedUtc = session.Started
            };
        }

        public SessionToken Get(string sessionToken)
        {
            var json = Base64Decode(sessionToken);
            return JSON.Parse<SessionToken>(json);
        }

        public async Task<bool> EndSessionAndRaidAsync(SessionToken token, string userIdOrUsername, bool isWarRaid)
        {
            var db = dbProvider.Get();

            var currentSession = await db.GameSession.FirstOrDefaultAsync(x => x.Id == token.SessionId);
            if (currentSession == null)
            {
                return false;
            }

            var user = await db.User.FirstOrDefaultAsync(x =>
                x.UserId == userIdOrUsername || x.UserName.ToLower().Equals(userIdOrUsername));

            if (user == null)
            {
                await EndSessionAsync(token);
                return false;
            }

            var targetSession = await db.GameSession.FirstOrDefaultAsync(x =>
                x.UserId == user.Id && x.Status == (int)SessionStatus.Active);

            if (targetSession == null)
            {
                await EndSessionAsync(token);
                return false;
            }

            var revision = 1;
            var lastEvent = await db.GameEvent.LastOrDefaultAsync(x => x.GameSessionId == targetSession.Id);
            if (lastEvent != null) revision = lastEvent.Revision + 1;

            var ge = new DataModels.GameEvent
            {
                Id = Guid.NewGuid(),
                GameSessionId = targetSession.Id,
                Revision = revision,
                Type = isWarRaid
                    ? (int)GameEventType.WarRaid
                    : (int)GameEventType.Raid,
                //Data = JsonConvert.SerializeObject(currentSession.CharacterSession.Select(x => x.CharacterId).ToArray())
            };

            await db.GameEvent.AddAsync(ge);
            await EndSessionAsync(token);
            return true;
        }

        public Task EndSessionAsync(SessionToken token)
        {
            return EndSessionAsync(token, dbProvider.Get());
        }

        private async Task EndSessionAsync(SessionToken token, RavenfallDbContext db)
        {
            var session = await db.GameSession.FirstOrDefaultAsync(x => x.Id == token.SessionId);
            if (session == null)
            {
                return;
            }

            //foreach (var charSession in db.CharacterSession.Where(x =>
            //    x.SessionId == token.SessionId &&
            //    x.Status == (int)SessionStatus.Active))
            //{
            //    charSession.Status = (int)SessionStatus.Inactive;
            //    charSession.Ended = DateTime.UtcNow;
            //    db.Update(charSession);
            //}

            session.Status = (int)SessionStatus.Inactive;
            session.Stopped = DateTime.UtcNow;
            db.Update(session);
            await db.SaveChangesAsync();
        }

        private static string Base64Decode(string str)
        {
            var data = System.Convert.FromBase64String(str);
            return Encoding.UTF8.GetString(data);
        }
    }
}