using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    public class SecureHasher : ISecureHasher
    {
        // Public in the source, so only good for password hashes (which must keep matching what is
        // stored) and for the fallback when no signing key is configured.
        private const string HASHKEY = "ravenfall_yayayayayayya_temp_key";

        private readonly byte[] tokenSigningKey;

        public SecureHasher(IOptions<AppSettings> settings, ILogger<SecureHasher> logger)
            : this(settings?.Value?.TokenSigningKey, logger)
        {
        }

        public SecureHasher(string tokenSigningKey, ILogger logger = null)
        {
            if (string.IsNullOrWhiteSpace(tokenSigningKey))
            {
                logger?.LogWarning("AppSettings:TokenSigningKey is not set. Auth and session tokens are signed with the "
                    + "constant from the public source code and can be forged by anyone. Add a long random "
                    + "TokenSigningKey to appsettings.json and restart. Every player logs in once more after that.");
                return;
            }

            if (tokenSigningKey.Length < 32)
            {
                logger?.LogWarning("AppSettings:TokenSigningKey is shorter than 32 characters. Use a long random value.");
            }

            this.tokenSigningKey = Encoding.UTF8.GetBytes(tokenSigningKey);
        }

        public bool HasTokenSigningKey => tokenSigningKey != null;

        public string Get(AuthToken token)
        {
            var userId = token.UserId;
            var expires = token.ExpiresUtc;
            var issued = token.IssuedUtc;
            return SignToken(
                userId + "|" +
                expires.ToString("g") + "|" +
                issued.ToString("g"));
        }

        public string Get(SessionToken token)
        {
            return SignToken(token.SessionId + "|" + token.AuthToken);
        }

        public string Get(string text)
        {
            return ComputeSha256Hash(text + "|" + HASHKEY);
        }

        public bool IsValid(AuthToken token)
        {
            if (token == null || string.IsNullOrEmpty(token.Token))
            {
                return false;
            }

            return FixedTimeEquals(token.Token, Get(token));
        }

        public string GetSessionSignature(Guid userId, Guid sessionId)
        {
            // The prefix keeps a session signature from ever being usable as an auth token hash.
            return SignToken("session|" + userId + "|" + sessionId);
        }

        public bool IsValidSessionSignature(SessionToken token)
        {
            if (token == null || string.IsNullOrEmpty(token.AuthToken))
            {
                return false;
            }

            return FixedTimeEquals(token.AuthToken, GetSessionSignature(token.UserId, token.SessionId));
        }

        private string SignToken(string text)
        {
            if (tokenSigningKey == null)
            {
                return Get(text);
            }

            using (var hmac = new HMACSHA256(tokenSigningKey))
            {
                return ToHex(hmac.ComputeHash(Encoding.UTF8.GetBytes(text)));
            }
        }

        private static bool FixedTimeEquals(string a, string b)
        {
            return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
        }

        private static string ComputeSha256Hash(string rawData)
        {
            using (var sha256Hash = SHA256.Create())
            {
                return ToHex(sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData)));
            }
        }

        private static string ToHex(byte[] bytes)
        {
            var builder = new StringBuilder();
            builder.AppendJoin("", bytes.Select(x => x.ToString("x2")));
            return builder.ToString();
        }
    }
}
