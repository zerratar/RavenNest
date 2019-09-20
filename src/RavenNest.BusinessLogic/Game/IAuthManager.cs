using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    public interface IAuthManager
    {
        Task<AuthToken> AuthenticateAsync(string username, string password);
        AuthToken Get(string authToken);
        Task SignUpAsync(string userId, string userLogin, string userDisplayName, string userEmail, string password);
    }

    public class AuthManager : IAuthManager
    {
        private readonly ILogger logger;
        private readonly IRavenfallDbContextProvider ctxProvider;
        private readonly ISecureHasher secureHash;

        public AuthManager(
            ILogger logger,
            IRavenfallDbContextProvider ctxProvider,
            ISecureHasher secureHash)
        {
            this.logger = logger;
            this.ctxProvider = ctxProvider;
            this.secureHash = secureHash;
        }

        public async Task SignUpAsync(
            string userId,
            string userLogin,
            string userDisplayName,
            string userEmail,
            string password)
        {
            try
            {
                using (var db = ctxProvider.Get())
                {
                    var user = await db.User
                        .FirstOrDefaultAsync(x => x.UserId.Equals(userId, StringComparison.OrdinalIgnoreCase));

                    if (user == null)
                    {
                        return;
                    }

                    if (!string.IsNullOrEmpty(user.PasswordHash))
                    {
                        return;
                    }

                    user.DisplayName = userDisplayName;
                    user.UserName = userLogin;
                    user.Email = userEmail;
                    user.PasswordHash = secureHash.Get(password);
                    db.Update(user);
                    await db.SaveChangesAsync();
                }
            }
            catch (Exception exc)
            {
                await logger.WriteErrorAsync(
                    $"Error saving user data for '{userDisplayName} ({userId})'! (EXCEPTION): " + exc);
            }
        }

        public async Task<AuthToken> AuthenticateAsync(string username, string password)
        {
            using (var db = ctxProvider.Get())
            {
                var user = await db.User
                    .FirstOrDefaultAsync(
                        x => x.UserName.Equals(username, StringComparison.OrdinalIgnoreCase));

                if (user == null)
                {
                    return null;
                }

                if (string.IsNullOrEmpty(user.PasswordHash))
                {
                    //return GenerateAuthToken(user);
                    return null; // you must now have a password.
                }

                var hashedPassword = secureHash.Get(password);
                if (user.PasswordHash.Equals(hashedPassword))
                {
                    return GenerateAuthToken(user);
                }

                return null;
            }
        }

        private AuthToken GenerateAuthToken(User user)
        {
            var userId = user.Id;
            var expires = DateTime.UtcNow + TimeSpan.FromHours(12);
            var issued = DateTime.UtcNow;
            var authToken = new AuthToken
            {
                UserId = userId,
                ExpiresUtc = expires,
                IssuedUtc = issued
            };

            authToken.Token = secureHash.Get(authToken);
            return authToken;
        }

        public AuthToken Get(string authToken)
        {
            var json = Base64Decode(authToken);
            return JSON.Parse<AuthToken>(json);
        }

        private static string Base64Decode(string str)
        {
            var data = System.Convert.FromBase64String(str);
            return Encoding.UTF8.GetString(data);
        }
    }
}