using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    public class AuthManager : IAuthManager
    {
        private readonly IGameData gameData;
        private readonly ILogger logger;
        private readonly ISecureHasher secureHash;

        public AuthManager(
            IGameData gameData,
            ILogger<AuthManager> logger,
            ISecureHasher secureHash)
        {
            this.gameData = gameData;
            this.logger = logger;
            this.secureHash = secureHash;
        }

        public bool IsAdmin(AuthToken authToken)
        {
            var user = gameData.GetUser(authToken.UserId);
            if (user == null) return false;
            if (user.IsAdmin.HasValue)
            {
                return user.IsAdmin.Value;
            }

            return false;
        }

        public void SignUp(
            string userId,
            string userLogin,
            string userDisplayName,
            string userEmail,
            string password)
        {
            try
            {
                var user = gameData.GetUserByTwitchId(userId);
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
                gameData.Flush();
            }
            catch (Exception exc)
            {
                logger.LogError($"Error saving user data for '{userDisplayName} ({userId})'! (EXCEPTION): " + exc);
            }
        }

        public AuthToken Authenticate(string username, string password)
        {
            var user = gameData.GetUserByUsername(username);
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

        public AuthToken GenerateAuthToken(User user)
        {
            var userId = user.Id;
            var expires = DateTime.UtcNow + TimeSpan.FromDays(180);
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

        public string GetRandomizedBase64EncodedStateParameters(List<StateParameters> stateParameters)
        {
            // TODO Should store this in a cookie or session to be compared to state response to prevent CSRF: https://datatracker.ietf.org/doc/html/rfc6749#section-10.12
            string randomSeed = Convert.ToBase64String(Guid.NewGuid().ToByteArray()).Replace("=", "");
            StateParameters randomizedStateParamater = new("state_token", randomSeed);
            stateParameters.Add(randomizedStateParamater);

            var serializedJSON = JsonConvert.SerializeObject(stateParameters);
            var encodedString = Base64UrlEncoder.Encode(serializedJSON);
            return encodedString;
        }

        public List<StateParameters> GetDecodedObjectFromState(string encodedState)
        {
            //TODO Decode - add check in to compare returned values and cause an error if different
            var decodedJSONString = Base64UrlEncoder.Decode(encodedState.Trim());
            return JsonConvert.DeserializeObject<List<StateParameters>>(decodedJSONString);
        }

    }
}
