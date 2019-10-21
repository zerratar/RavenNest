using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Net;
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
            var user = await db.User.FirstOrDefaultAsync(x => x.Id == token.UserId);
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
                await EndSessionAsync(db, activeSession);
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

            if (user.IsAdmin.GetValueOrDefault() || user.IsModerator.GetValueOrDefault())
            {
                var permissionEvent = new DataModels.GameEvent
                {
                    Id = Guid.NewGuid(),
                    GameSession = newGameSession,
                    GameSessionId = newGameSession.Id,
                    Type = (int)GameEventType.PermissionChange,
                    Revision = 1,
                    Data = JSON.Stringify(new Permissions
                    {
                        IsAdministrator = user.IsAdmin ?? false,
                        IsModerator = user.IsModerator ?? false
                    })
                };
                await db.GameEvent.AddAsync(permissionEvent);
            }

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

        public async Task<bool> EndSessionAndRaidAsync(
            SessionToken token, string userIdOrUsername, bool isWarRaid)
        {
            var db = dbProvider.Get();

            var currentSession = await db.GameSession.Include(x => x.User).FirstOrDefaultAsync(x => x.Id == token.SessionId);
            if (currentSession == null)
            {
                return false;
            }

            var user = await db.User.FirstOrDefaultAsync(x =>
                x.UserId == userIdOrUsername || x.UserName.ToLower().Equals(userIdOrUsername));

            if (user == null)
            {
                //await EndSessionAsync(token);
                return false;
            }

            var targetSession = await db.GameSession.FirstOrDefaultAsync(x =>
                x.UserId == user.Id && x.Status == (int)SessionStatus.Active);

            if (targetSession == null)
            {
                //await EndSessionAsync(token);
                return false;
            }

            var revision = 1;
            var lastEvent = await db.GameEvent.LastOrDefaultAsync(x => x.GameSessionId == targetSession.Id);
            if (lastEvent != null) revision = lastEvent.Revision + 1;

            var characters = await db.Character
                .Include(x => x.User)
                .Where(x => x.UserIdLock == currentSession.UserId && x.LastUsed != null && x.LastUsed >= currentSession.Started)
                .OrderByDescending(x => x.LastUsed)
                .ToListAsync();

            var ge = new DataModels.GameEvent
            {
                Id = Guid.NewGuid(),
                GameSessionId = targetSession.Id,
                GameSession = targetSession,
                Revision = revision,
                Type = isWarRaid
                    ? (int)GameEventType.WarRaid
                    : (int)GameEventType.Raid,
                Data = JsonConvert.SerializeObject(new
                {
                    RaiderUserName = currentSession.User.UserName,
                    RaiderUserId = currentSession.User.UserId,
                    Players = characters.Select(x => x.User.UserId).ToArray()
                })
            };

            await db.GameEvent.AddAsync(ge);
            await EndSessionAsync(token);
            await db.SaveChangesAsync();
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

            await EndSessionAsync(db, session);
        }

        private static async Task EndSessionAsync(RavenfallDbContext db, GameSession session)
        {
            var characters = await db.Character
                .Where(x => x.UserIdLock == session.UserId && x.LastUsed != null && x.LastUsed >= session.Started)
                .OrderByDescending(x => x.LastUsed)
                .ToListAsync();

            foreach (var character in characters)
            {
                character.UserIdLock = null;
                db.Update(character);
            }

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