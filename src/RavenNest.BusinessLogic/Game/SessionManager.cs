using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Net;
using RavenNest.DataModels;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    public class SessionManager : ISessionManager
    {
        private readonly IGameData gameData;

        public SessionManager(IGameData gameData) // IRavenfallDbContextProvider dbProvider
        {
            this.gameData = gameData;
        }

        public SessionToken BeginSession(AuthToken token, string clientVersion, string accessKey, bool isLocal)
        {
            var game = gameData.Client;
            var user = gameData.GetUser(token.UserId);

            if (game == null)
            {
                return null;
            }

            if (game.AccessKey != accessKey)
            {
                return null;
            }

            var userId = token.UserId;
            var activeSession = gameData.GetUserSession(userId);
            // x => x.UserId == userId && x.Status == (int)SessionStatus.Active

            if (activeSession != null)
            {
                EndSession(activeSession);
            }

            var newGameSession = gameData.CreateSession(userId);

            gameData.Add(newGameSession);

            if (user.IsAdmin.GetValueOrDefault() || user.IsModerator.GetValueOrDefault())
            {
                DataModels.GameEvent permissionEvent = gameData.CreateSessionEvent(
                    GameEventType.PermissionChange,
                    newGameSession,
                    new Permissions
                    {
                        IsAdministrator = user.IsAdmin ?? false,
                        IsModerator = user.IsModerator ?? false
                    });

                gameData.Add(permissionEvent);
            }


            return GenerateSessionToken(token, newGameSession);
        }

        public bool EndSessionAndRaid(
            SessionToken token, string userIdOrUsername, bool isWarRaid)
        {
            var currentSession = gameData.GetSession(token.SessionId);
            if (currentSession == null)
            {
                return false;
            }

            var sessionUser = gameData.GetUser(currentSession.UserId);
            var user = gameData.FindUser(userIdOrUsername);
            if (user == null)
            {
                //await EndSessionAsync(token);
                return false;
            }

            var targetSession = gameData.GetUserSession(user.Id);
            if (targetSession == null)
            {
                //await EndSessionAsync(token);
                return false;
            }


            //var lastEvent = gameData.GetNextEventRevision(x => x.GameSessionId == targetSession.Id);
            //if (lastEvent != null) revision = lastEvent.Revision + 1;


            //var characters = await db.Character
            //    .Include(x => x.User)
            //    .Where(x => x.UserIdLock == currentSession.UserId && x.LastUsed != null && x.LastUsed >= currentSession.Started)
            //    .OrderByDescending(x => x.LastUsed)
            //    .ToListAsync();

            var characters = gameData.GetSessionCharacters(currentSession);

            //  var revision = gameData.GetNextGameEventRevision(targetSession.Id);

            var ge = gameData.CreateSessionEvent(
                isWarRaid ? GameEventType.WarRaid : GameEventType.Raid,
                targetSession, new
                {
                    RaiderUserName = sessionUser.UserName,
                    RaiderUserId = sessionUser.UserId,
                    Players = characters.Select(x => gameData.GetUser(x.UserId).UserId).ToArray()
                });

            //var ge = new DataModels.GameEvent
            //{
            //    Id = Guid.NewGuid(),
            //    GameSessionId = targetSession.Id,
            //    GameSession = targetSession,
            //    Revision = revision,
            //    Type = isWarRaid
            //        ? (int)GameEventType.WarRaid
            //        : (int)GameEventType.Raid,
            //    Data = JsonConvert.SerializeObject(new
            //    {
            //        RaiderUserName = currentSession.User.UserName,
            //        RaiderUserId = currentSession.User.UserId,
            //        Players = characters.Select(x => x.User.UserId).ToArray()
            //    })
            //};

            gameData.Add(ge);
            EndSession(token);
            return true;
        }

        public void EndSession(SessionToken token)
        {
            var session = gameData.GetSession(token.SessionId);
            if (session == null)
            {
                return;
            }

            EndSession(session);
        }

        public void EndSession(GameSession session)
        {
            var characters = gameData.GetSessionCharacters(session);

            foreach (var character in characters)
            {
                character.UserIdLock = null;
            }

            session.Status = (int)SessionStatus.Inactive;
            session.Stopped = DateTime.UtcNow;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SessionToken Get(string sessionToken)
        {
            var json = Base64Decode(sessionToken);
            return JSON.Parse<SessionToken>(json);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string Base64Decode(string str)
        {
            var data = System.Convert.FromBase64String(str);
            return Encoding.UTF8.GetString(data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
    }
}