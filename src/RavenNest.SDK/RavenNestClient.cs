using System;
using System.Threading.Tasks;
using RavenNest.Models;
using RavenNest.SDK.Endpoints;

namespace RavenNest.SDK
{
    public class RavenNestClient : IRavenNestClient
    {
        private readonly ILogger logger;
        private readonly IApiRequestBuilderProvider request;
        private readonly IAppSettings appSettings;

        private AuthToken currentAuthToken;
        private SessionToken currentSessionToken;

        public RavenNestClient(ILogger logger, IAppSettings settings)
        {
            this.logger = logger ?? new ConsoleLogger();
            this.appSettings = settings ?? new RavenNestStreamSettings();
            this.request = new WebApiRequestBuilderProvider(this.appSettings);
            Auth = new WebBasedAuthEndpoint(this, logger, request);
            Game = new WebBasedGameEndpoint(this, logger, request);
            Items = new WebBasedItemsEndpoint(this, logger, request);
            Players = new WebBasedPlayersEndpoint(this, logger, request);
        }

        public IAuthEndpoint Auth { get; }
        public IGameEndpoint Game { get; }
        public IItemEndpoint Items { get; }
        public IPlayerEndpoint Players { get; }

        public async Task<bool> LoginAsync(string username, string password)
        {
            var authToken = await this.Auth.AuthenticateAsync(username, password);
            if (authToken != null)
            {
                this.currentAuthToken = authToken;
                this.request.SetAuthToken(this.currentAuthToken);
                return true;
            }
            return false;
        }

        public async Task<bool> StartSessionAsync(bool useLocalPlayers)
        {
            var sessionToken = await this.Game.BeginSessionAsync(useLocalPlayers);
            if (sessionToken != null)
            {
                this.currentSessionToken = sessionToken;
                this.request.SetSessionToken(this.currentSessionToken);
                return true;
            }
            return false;
        }

        public async Task<Player> PlayerJoinAsync(string userId, string username)
        {
            try
            {
                return await this.Players.PlayerJoinAsync(userId, username);
            }
            catch
            {
                return null;
            }
        }

        public async Task<bool> EndSessionAndRaidAsync(string username, bool war)
        {
            try
            {
                return await this.Game.EndSessionAndRaidAsync(username, war);
            }
            catch
            {
                return false;
            }
            finally
            {
                this.currentSessionToken = null;
                this.request.SetSessionToken(null);
            }
        }

        public async Task<bool> EndSessionAsync()
        {
            try
            {
                await this.Game.EndSessionAsync();
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                this.currentSessionToken = null;
                this.request.SetSessionToken(null);
            }
        }


        public bool Authenticated => currentAuthToken != null &&
                                       currentAuthToken.UserId != Guid.Empty &&
                                       !currentAuthToken.Expired;

        public bool SessionStarted => currentSessionToken != null &&
                                      !string.IsNullOrEmpty(currentSessionToken.AuthToken) &&
                                      !currentSessionToken.Expired;
    }
}