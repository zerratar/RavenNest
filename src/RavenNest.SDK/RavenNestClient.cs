using System;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using RavenNest.Models;
using RavenNest.SDK.Endpoints;

namespace RavenNest.SDK
{
    public class RavenNestClient : IRavenNestClient
    {
        private readonly ILogger logger;
        private readonly IAppSettings appSettings;
        private readonly ITokenProvider tokenProvider;

        private readonly IGameManager gameManager;
        private AuthToken currentAuthToken;
        private SessionToken currentSessionToken;

        private int activeRequestCount;
        private int updateCounter;
        private int badClientVersion;

        public bool BadClientVersion => Volatile.Read(ref badClientVersion) == 1;

        public RavenNestClient(
            ILogger logger,
            IGameManager gameManager,
            IAppSettings settings,
            IGameCache cache)
        {
            ServicePointManager.DefaultConnectionLimit = 100;
            ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(ValidateCertificate);
            //ServicePointManager.CertificatePolicy = new NoCheckCertificatePolicy();

            this.logger = logger ?? new ConsoleLogger();//new UnityLogger();
            this.gameManager = gameManager;
            var binarySerializer = new CompressedJsonSerializer();//new BinarySerializer();
            appSettings = settings ?? new ProductionRavenNestStreamSettings();

            tokenProvider = new TokenProvider();
            var request = new WebApiRequestBuilderProvider(logger, appSettings, tokenProvider);

            Stream = new WebSocketEndpoint(this, gameManager, logger, settings, tokenProvider, new GamePacketSerializer(binarySerializer), cache);
            Auth = new WebBasedAuthEndpoint(this, logger, request);
            Game = new WebBasedGameEndpoint(this, logger, request);
            Items = new WebBasedItemsEndpoint(this, logger, request);
            Players = new WebBasedPlayersEndpoint(this, logger, request);
            Marketplace = new WebBasedMarketplaceEndpoint(this, logger, request);
            Village = new WebBasedVillageEndpoint(this, logger, request);
            Admin = new WebBasedAdminEndpoint(this, logger, request);
        }

        public IWebSocketEndpoint Stream { get; }
        public IAdminEndpoint Admin { get; }
        public IAuthEndpoint Auth { get; }
        public IGameEndpoint Game { get; }
        public IItemEndpoint Items { get; }
        public IPlayerEndpoint Players { get; }
        public IMarketplaceEndpoint Marketplace { get; }

        public IVillageEndpoint Village { get; }

        public bool Authenticated => currentAuthToken != null &&
                                       currentAuthToken.UserId != Guid.Empty &&
                                       !currentAuthToken.Expired;

        public bool SessionStarted => currentSessionToken != null &&
                                      !string.IsNullOrEmpty(currentSessionToken.AuthToken) &&
                                      !currentSessionToken.Expired;

        public bool HasActiveRequest => activeRequestCount > 0;

        public string ServerAddress => appSettings.ApiEndpoint;
        public Guid SessionId { get; private set; }
        public string TwitchUserName { get; private set; }
        public string TwitchDisplayName { get; private set; }
        public string TwitchUserId { get; private set; }

        public bool Desynchronized { get; set; }
        public async Task UpdateAsync()
        {
            if (Desynchronized) return;
            if (!SessionStarted)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref updateCounter, 1, 0) == 1)
            {
                return;
            }

            if (!await Stream.UpdateAsync())
            {
                logger.Debug("Reconnecting to server...");
            }

            Interlocked.Decrement(ref updateCounter);
        }
        public void SendPlayerLoyaltyData(IPlayerController player)
        {
            if (Desynchronized) return;

            if (player == null)
            {
                return;
            }

            if (!SessionStarted)
            {
                return;
            }

            Stream.SendPlayerLoyaltyData(player);
        }

        public async Task<bool> SavePlayerAsync(IPlayerController player)
        {
            if (Desynchronized) return false;
            if (player == null)
            {
                return false;
            }

            if (!SessionStarted)
            {
                logger.Debug("Trying to save player " + player.PlayerName + " but session has not been started.");
                return false;
            }

            var saveResult = await Stream.SavePlayerSkillsAsync(player);
            await Stream.SavePlayerStateAsync(player);
            return saveResult;
        }

        public async Task<bool> LoginAsync(string username, string password)
        {
            if (Desynchronized) return false;
            try
            {
                Interlocked.Increment(ref activeRequestCount);
                var authToken = await Auth.AuthenticateAsync(username, password);
                if (authToken != null)
                {
                    currentAuthToken = authToken;
                    tokenProvider.SetAuthToken(currentAuthToken);
                    gameManager.OnAuthenticated();
                    return true;
                }
            }
            catch (Exception exc)
            {
                logger.Error(exc.ToString());

            }
            finally
            {
                Interlocked.Decrement(ref activeRequestCount);
            }
            return false;
        }

        public async Task<bool> StartSessionAsync(string clientVersion, string accessKey, bool useLocalPlayers)
        {
            if (Desynchronized) return false;
            try
            {
                Interlocked.Increment(ref activeRequestCount);
                var sessionToken = await Game.BeginSessionAsync(clientVersion, accessKey, useLocalPlayers, 0);
                if (sessionToken != null)
                {
                    tokenProvider.SetSessionToken(sessionToken);
                    currentSessionToken = sessionToken;
                    SessionId = currentSessionToken.SessionId;
                    TwitchUserName = currentSessionToken.TwitchUserName;
                    TwitchDisplayName = currentSessionToken.TwitchDisplayName;
                    TwitchUserId = currentSessionToken.TwitchUserId;
                    gameManager.OnSessionStart();
                    return true;
                }
                else
                {
                    Interlocked.CompareExchange(ref badClientVersion, 1, 0);
                }

                await Task.Delay(250);
            }
            catch (Exception exc)
            {
                logger.Error(exc.ToString());
            }
            finally
            {
                Interlocked.Decrement(ref activeRequestCount);
            }
            return false;
        }

        internal async void PlayerRemoveAsync(IPlayerController player)
        {
            if (Desynchronized) return;
            try
            {
                Interlocked.Increment(ref activeRequestCount);
                await Players.PlayerRemoveAsync(player.Id);
            }
            catch (Exception exc)
            {
                logger.Error(exc.ToString());
            }
            finally
            {
                Interlocked.Decrement(ref activeRequestCount);
            }
        }

        public async Task<RavenNest.Models.PlayerJoinResult> PlayerJoinAsync(PlayerJoinData joinData)
        {
            if (Desynchronized) return null;
            try
            {
                Interlocked.Increment(ref activeRequestCount);
                return await Players.PlayerJoinAsync(joinData);
            }
            catch (Exception exc)
            {
                logger.Error(exc.ToString());
                return null;
            }
            finally
            {
                Interlocked.Decrement(ref activeRequestCount);
            }
        }

        public async Task<bool> EndSessionAndRaidAsync(string username, bool war)
        {
            try
            {
                Interlocked.Increment(ref activeRequestCount);
                return await Game.EndSessionAndRaidAsync(username, war);
            }
            catch (Exception exc)
            {
                logger.Error(exc.ToString());
                return false;
            }
            finally
            {
                Interlocked.Decrement(ref activeRequestCount);
                currentSessionToken = null;
                tokenProvider.SetSessionToken(null);
            }
        }

        public async Task<bool> EndSessionAsync()
        {
            try
            {
                Interlocked.Increment(ref activeRequestCount);
                await Game.EndSessionAsync();
                return true;
            }
            catch (Exception exc)
            {
                logger.Error(exc.ToString());
                return false;
            }
            finally
            {
                Stream.Close();
                Interlocked.Decrement(ref activeRequestCount);
                currentSessionToken = null;
                tokenProvider.SetSessionToken(null);
            }
        }
        private bool ValidateCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }
    }
}
