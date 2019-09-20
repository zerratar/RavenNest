using System.Linq;
using System.Security.Cryptography;
using System.Text;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    public class SecureHasher : ISecureHasher
    {
        private const string HASHKEY = "ravenfall_yayayayayayya_temp_key";

        public string Get(AuthToken token)
        {
            var userId = token.UserId;
            var expires = token.ExpiresUtc;
            var issued = token.IssuedUtc;
            return Get(
                userId + "|" +
                expires.ToString("g") + "|" +
                issued.ToString("g"));
        }

        public string Get(SessionToken token)
        {
            return Get(token.SessionId + "|" + token.AuthToken);
        }

        public string Get(string text)
        {
            return ComputeSha256Hash(text + "|" + HASHKEY);
        }

        private static string ComputeSha256Hash(string rawData)
        {
            // Create a SHA256   
            var builder = new StringBuilder();
            using (var sha256Hash = SHA256.Create())
            {
                // ComputeHash - returns byte array  
                var bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));

                // Convert byte array to a string                   
                builder.AppendJoin("", bytes.Select(x => x.ToString("x2")));

                return builder.ToString();
            }
        }
    }
}