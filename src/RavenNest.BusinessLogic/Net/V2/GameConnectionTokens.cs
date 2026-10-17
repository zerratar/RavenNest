using System;
using System.Buffers.Binary;
using System.Security.Cryptography;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Net.V2
{
    /// <summary>
    /// The token a game presents in <see cref="Protocol.Hello"/>: <c>[version:1][session:16][user:16][expires:8]</c>
    /// followed by an HMAC-SHA256 of those bytes, base64url, 98 characters. The server checks it without a lookup
    /// and nobody can make one without the signing key. Handed out with BeginSession; v2 only, old clients keep
    /// their old token.
    /// </summary>
    public sealed class GameConnectionTokens
    {
        private const byte FormatVersion = 1;
        private const int PayloadLength = 1 + 16 + 16 + 8;
        private const int SignatureLength = 32;

        private readonly ISecureHasher hasher;
        private readonly GameData gameData;

        public GameConnectionTokens(ISecureHasher hasher, GameData gameData)
        {
            this.hasher = hasher;
            this.gameData = gameData;
        }

        public string Issue(Guid sessionId, Guid userId, DateTime expiresUtc)
        {
            var token = new byte[PayloadLength + SignatureLength];
            token[0] = FormatVersion;
            sessionId.TryWriteBytes(token.AsSpan(1, 16));
            userId.TryWriteBytes(token.AsSpan(17, 16));
            BinaryPrimitives.WriteInt64LittleEndian(token.AsSpan(33, 8), new DateTimeOffset(expiresUtc, TimeSpan.Zero).ToUnixTimeSeconds());
            hasher.SignBytes(token.AsSpan(0, PayloadLength).ToArray()).CopyTo(token, PayloadLength);
            return Base64UrlEncode(token);
        }

        public string Issue(SessionToken sessionToken)
        {
            return Issue(sessionToken.SessionId, sessionToken.UserId, sessionToken.ExpiresUtc);
        }

        /// <summary>
        /// Checks the token and builds the session token the rest of the server works with. False when the token
        /// is malformed, not signed by this server, expired, or its session is gone or belongs to someone else.
        /// </summary>
        public bool TryValidate(string text, string clientVersion, out SessionToken sessionToken, out string error)
        {
            sessionToken = null;
            byte[] token;
            try
            {
                token = Base64UrlDecode(text);
            }
            catch (FormatException)
            {
                error = "not a token";
                return false;
            }

            if (token.Length != PayloadLength + SignatureLength || token[0] != FormatVersion)
            {
                error = "not a token";
                return false;
            }

            var expected = hasher.SignBytes(token.AsSpan(0, PayloadLength).ToArray());
            if (!CryptographicOperations.FixedTimeEquals(expected, token.AsSpan(PayloadLength, SignatureLength)))
            {
                error = "bad signature";
                return false;
            }

            var sessionId = new Guid(token.AsSpan(1, 16));
            var userId = new Guid(token.AsSpan(17, 16));
            var expires = DateTimeOffset.FromUnixTimeSeconds(BinaryPrimitives.ReadInt64LittleEndian(token.AsSpan(33, 8))).UtcDateTime;
            if (expires <= DateTime.UtcNow)
            {
                error = "expired";
                return false;
            }

            var session = gameData.GetSession(sessionId, false);
            if (session == null || session.UserId != userId)
            {
                error = session == null ? "no such session" : "session belongs to another user";
                return false;
            }

            var user = gameData.GetUser(userId);
            if (user == null)
            {
                error = "no such user";
                return false;
            }

            sessionToken = new SessionToken
            {
                SessionId = sessionId,
                UserId = userId,
                StartedUtc = session.Started,
                ExpiresUtc = expires,
                AuthToken = hasher.GetSessionSignature(userId, sessionId),
                UserName = user.UserName,
                DisplayName = user.DisplayName,
                ClientVersion = clientVersion
            };
            error = null;
            return true;
        }

        private static string Base64UrlEncode(byte[] data)
        {
            return Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private static byte[] Base64UrlDecode(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                throw new FormatException();
            }

            var s = text.Replace('-', '+').Replace('_', '/');
            switch (s.Length % 4)
            {
                case 2: s += "=="; break;
                case 3: s += "="; break;
                case 1: throw new FormatException();
            }
            return Convert.FromBase64String(s);
        }
    }
}
