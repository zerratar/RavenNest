using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    /// <summary>
    /// Signing the game client in through the website, and letting it sign in again on its own.
    /// </summary>
    /// <remarks>
    /// The sign in works like a TV signing in to a streaming service:
    /// <list type="number">
    /// <item>The client makes a secret, starts a request with it and gets a short code back.</item>
    /// <item>It opens the website at that code. The streamer signs in there (with Twitch), sees
    /// the same code the game shows, and approves.</item>
    /// <item>The client asks with its code and secret until the request is approved, and then
    /// gets an auth token and a device token.</item>
    /// </list>
    /// The code alone is not enough to collect the result: it is shown on screen, so a viewer
    /// could read it off the stream, but only the client knows the secret.
    ///
    /// <para>
    /// A device token is the id of a <see cref="ClientDevice"/> and a secret, "id.secret". The
    /// client keeps it in place of a password and trades it for an auth token at start. Only a
    /// hash of the secret is stored, a device unused for <see cref="DeviceLifetime"/> lapses, and a
    /// device can be revoked. Requests waiting for approval live in memory: they last minutes.
    /// </para>
    /// </remarks>
    public class ClientLoginManager
    {
        public static readonly TimeSpan RequestLifetime = TimeSpan.FromMinutes(10);
        public static readonly TimeSpan DeviceLifetime = TimeSpan.FromDays(90);

        // no 0/O, 1/I/L: the code is read off one screen and compared with another
        private const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
        private const int MaxWrongSecrets = 10;
        private const int MaxPendingRequests = 5000;

        private readonly GameData gameData;
        private readonly IAuthManager authManager;
        private readonly ILogger<ClientLoginManager> logger;
        private readonly ConcurrentDictionary<string, Request> requests = new ConcurrentDictionary<string, Request>();

        public ClientLoginManager(GameData gameData, IAuthManager authManager, ILogger<ClientLoginManager> logger)
        {
            this.gameData = gameData;
            this.authManager = authManager;
            this.logger = logger;
        }

        public enum RequestStatus { Pending, Approved, Denied, Expired, Unknown }

        public sealed class Request
        {
            public string Code;
            public string SecretHash;
            public string DeviceName;
            public DateTime Created;
            public Guid? ApprovedBy;
            public bool Denied;
            public int WrongSecrets;
            public bool Expired => DateTime.UtcNow - Created > RequestLifetime;
        }

        public sealed class PollResult
        {
            public RequestStatus Status;
            public AuthToken AuthToken;
            public string DeviceToken;
            public string UserName;
        }

        public sealed class DeviceLoginResult
        {
            public AuthToken AuthToken;
            public string UserName;
        }

        // ---- The sign in through the website -------------------------------------------------

        /// <summary>Starts a request for the client holding <paramref name="secret"/>. Returns its code, or null.</summary>
        public string Start(string secret, string deviceName)
        {
            if (string.IsNullOrEmpty(secret) || secret.Length < 32 || secret.Length > 256)
            {
                return null;
            }

            Sweep();
            if (requests.Count >= MaxPendingRequests)
            {
                logger.LogWarning("Client login: too many pending requests, refusing a new one.");
                return null;
            }

            for (var attempt = 0; attempt < 10; ++attempt)
            {
                var request = new Request
                {
                    Code = NewCode(),
                    SecretHash = Hash(secret),
                    DeviceName = Clean(deviceName),
                    Created = DateTime.UtcNow,
                };

                if (requests.TryAdd(request.Code, request))
                {
                    return request.Code;
                }
            }

            return null;
        }

        /// <summary>The request behind a code, for the approval page, or null if there is none.</summary>
        public Request Find(string code)
        {
            code = NormalizeCode(code);
            if (code == null || !requests.TryGetValue(code, out var request) || request.Expired)
            {
                return null;
            }

            return request;
        }

        /// <summary>The signed in website user approves the request. False when it is gone or settled.</summary>
        public bool Approve(string code, Guid userId)
        {
            var request = Find(code);
            var user = userId != Guid.Empty ? gameData.GetUser(userId) : null;
            if (request == null || user == null || IsBanned(user) || request.Denied || request.ApprovedBy.HasValue)
            {
                return false;
            }

            request.ApprovedBy = userId;
            logger.LogInformation("Client login: " + user.UserName + " approved a sign in for '" + request.DeviceName + "'.");
            return true;
        }

        /// <summary>The website user says the request was not theirs.</summary>
        public bool Deny(string code)
        {
            var request = Find(code);
            if (request == null || request.ApprovedBy.HasValue)
            {
                return false;
            }

            request.Denied = true;
            return true;
        }

        /// <summary>
        /// What the client gets when it asks about its request. Approval hands out the tokens once
        /// and ends the request; asking with the wrong secret counts against it.
        /// </summary>
        public PollResult Poll(string code, string secret)
        {
            code = NormalizeCode(code);
            if (code == null || !requests.TryGetValue(code, out var request))
            {
                return new PollResult { Status = RequestStatus.Unknown };
            }

            if (request.Expired)
            {
                requests.TryRemove(code, out _);
                return new PollResult { Status = RequestStatus.Expired };
            }

            if (!SameHash(request.SecretHash, Hash(secret)))
            {
                // someone guessing, or the code read off a stream: close the request after a few
                if (++request.WrongSecrets >= MaxWrongSecrets)
                {
                    requests.TryRemove(code, out _);
                }
                return new PollResult { Status = RequestStatus.Unknown };
            }

            if (request.Denied)
            {
                requests.TryRemove(code, out _);
                return new PollResult { Status = RequestStatus.Denied };
            }

            if (!request.ApprovedBy.HasValue)
            {
                return new PollResult { Status = RequestStatus.Pending };
            }

            if (!requests.TryRemove(code, out _))
            {
                return new PollResult { Status = RequestStatus.Unknown };
            }

            var user = gameData.GetUser(request.ApprovedBy.Value);
            if (user == null || IsBanned(user))
            {
                return new PollResult { Status = RequestStatus.Denied };
            }

            return new PollResult
            {
                Status = RequestStatus.Approved,
                AuthToken = authManager.GenerateAuthToken(user),
                DeviceToken = CreateDevice(user, request.DeviceName, "twitch"),
                UserName = user.UserName,
            };
        }

        // ---- Devices ---------------------------------------------------------------------------

        /// <summary>
        /// A device token for a client that is already signed in, as remember me does after a
        /// password sign in, so the client keeps this instead of the password.
        /// </summary>
        public string Register(AuthToken authToken, string deviceName)
        {
            var user = authToken != null ? gameData.GetUser(authToken.UserId) : null;
            if (user == null || IsBanned(user))
            {
                return null;
            }

            return CreateDevice(user, Clean(deviceName), "password");
        }

        /// <summary>Signs in with a device token, and keeps the device from lapsing. Null when it is not valid.</summary>
        public DeviceLoginResult Login(string deviceToken)
        {
            var device = Resolve(deviceToken);
            if (device == null)
            {
                return null;
            }

            var user = gameData.GetUser(device.UserId);
            if (user == null || IsBanned(user))
            {
                return null;
            }

            var now = DateTime.UtcNow;
            device.LastUsed = now;
            device.Expires = now + DeviceLifetime;
            return new DeviceLoginResult { AuthToken = authManager.GenerateAuthToken(user), UserName = user.UserName };
        }

        /// <summary>Forgets the device behind a token, as logging out in the game does.</summary>
        public bool Revoke(string deviceToken)
        {
            var device = Resolve(deviceToken);
            if (device == null)
            {
                return false;
            }

            gameData.Remove(device);
            return true;
        }

        /// <summary>Forgets one of a user's devices, from the website's list of them.</summary>
        public bool Revoke(Guid userId, Guid deviceId)
        {
            var device = gameData.GetClientDevice(deviceId);
            if (device == null || device.UserId != userId)
            {
                return false;
            }

            gameData.Remove(device);
            return true;
        }

        /// <summary>A user's devices, newest use first, with lapsed ones cleared away.</summary>
        public ClientDevice[] GetDevices(Guid userId)
        {
            var now = DateTime.UtcNow;
            var devices = gameData.GetClientDevices(userId).ToArray();
            foreach (var lapsed in devices.Where(x => x.Expires < now))
            {
                gameData.Remove(lapsed);
            }

            return devices.Where(x => x.Expires >= now).OrderByDescending(x => x.LastUsed).ToArray();
        }

        private string CreateDevice(User user, string deviceName, string platform)
        {
            var secret = NewSecret();
            var now = DateTime.UtcNow;
            var device = new ClientDevice
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Name = string.IsNullOrEmpty(deviceName) ? "Ravenfall" : deviceName,
                Platform = platform,
                SecretHash = Hash(secret),
                Created = now,
                LastUsed = now,
                Expires = now + DeviceLifetime,
            };

            gameData.Add(device);
            return device.Id.ToString("N") + "." + secret;
        }

        private ClientDevice Resolve(string deviceToken)
        {
            if (string.IsNullOrEmpty(deviceToken) || deviceToken.Length > 300)
            {
                return null;
            }

            var dot = deviceToken.IndexOf('.');
            if (dot <= 0 || !Guid.TryParseExact(deviceToken.Substring(0, dot), "N", out var id))
            {
                return null;
            }

            var device = gameData.GetClientDevice(id);
            if (device == null || !SameHash(device.SecretHash, Hash(deviceToken.Substring(dot + 1))))
            {
                return null;
            }

            if (device.Expires < DateTime.UtcNow)
            {
                gameData.Remove(device);
                return null;
            }

            return device;
        }

        // ---- Helpers ---------------------------------------------------------------------------

        private void Sweep()
        {
            foreach (var kv in requests)
            {
                if (kv.Value.Expired) requests.TryRemove(kv.Key, out _);
            }
        }

        private static bool IsBanned(User user) => user.Status.GetValueOrDefault() >= 1;

        private static string NewCode()
        {
            var chars = new char[8];
            for (var i = 0; i < chars.Length; ++i)
            {
                chars[i] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
            }

            return new string(chars);
        }

        /// <summary>Upper case without the dash, so "abcd-efgh" and "ABCDEFGH" find the same request.</summary>
        public static string NormalizeCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;
            var normalized = new string(code.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
            return normalized.Length == 8 ? normalized : null;
        }

        /// <summary>"ABCDEFGH" as "ABCD-EFGH", the way both screens show it.</summary>
        public static string FormatCode(string code)
        {
            code = NormalizeCode(code);
            return code == null ? string.Empty : code.Substring(0, 4) + "-" + code.Substring(4);
        }

        private static string NewSecret()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private static string Hash(string value)
        {
            using var sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty)));
        }

        private static bool SameHash(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(a), Encoding.ASCII.GetBytes(b));
        }

        private static string Clean(string deviceName)
        {
            if (string.IsNullOrWhiteSpace(deviceName)) return "Ravenfall";
            var name = new string(deviceName.Where(c => !char.IsControl(c)).ToArray()).Trim();
            return name.Length > 64 ? name.Substring(0, 64) : name;
        }
    }
}
