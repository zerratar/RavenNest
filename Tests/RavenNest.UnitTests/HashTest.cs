using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RavenNest.BusinessLogic;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using GameSession = RavenNest.DataModels.GameSession;

namespace RavenNest.UnitTests
{
    [TestClass]
    public class HashTest
    {
        private const string TestKey = "unit-test-signing-key-that-is-long-enough-0123456789";
        private const string OldConstant = "ravenfall_yayayayayayya_temp_key";

        [TestMethod]
        public void ValidAuthToken_IsAccepted()
        {
            var hasher = new SecureHasher(TestKey);
            var token = CreateAuthToken(hasher, Guid.NewGuid());

            Assert.IsTrue(hasher.HasTokenSigningKey);
            Assert.IsTrue(hasher.IsValid(token));
        }

        [TestMethod]
        public void ConfiguredFromAppSettings_SignsWithKey()
        {
            var hasher = new SecureHasher(Options.Create(new AppSettings { TokenSigningKey = TestKey }), null);
            var token = CreateAuthToken(hasher, Guid.NewGuid());

            Assert.IsTrue(hasher.HasTokenSigningKey);
            Assert.IsTrue(new SecureHasher(TestKey).IsValid(token));
        }

        [TestMethod]
        public void TamperedAuthToken_IsRejected()
        {
            var hasher = new SecureHasher(TestKey);
            var token = CreateAuthToken(hasher, Guid.NewGuid());

            var otherUser = Copy(token);
            otherUser.UserId = Guid.NewGuid();
            Assert.IsFalse(hasher.IsValid(otherUser));

            var longerExpiry = Copy(token);
            longerExpiry.ExpiresUtc = token.ExpiresUtc.AddYears(5);
            Assert.IsFalse(hasher.IsValid(longerExpiry));

            var changedHash = Copy(token);
            changedHash.Token = (token.Token[0] == 'a' ? "b" : "a") + token.Token.Substring(1);
            Assert.IsFalse(hasher.IsValid(changedHash));

            var noHash = Copy(token);
            noHash.Token = null;
            Assert.IsFalse(hasher.IsValid(noHash));
            Assert.IsFalse(hasher.IsValid(null));
        }

        [TestMethod]
        public void TokenSignedWithOldConstant_IsRejectedWhenKeyConfigured()
        {
            var legacy = new SecureHasher((string)null);
            var token = CreateAuthToken(legacy, Guid.NewGuid());

            Assert.IsTrue(legacy.IsValid(token));
            Assert.IsFalse(new SecureHasher(TestKey).IsValid(token));
            Assert.IsFalse(new SecureHasher("a-different-key-that-is-also-long-enough-0123456789").IsValid(CreateAuthToken(new SecureHasher(TestKey), Guid.NewGuid())));
        }

        [TestMethod]
        public void MissingKey_FallsBackToOldSigningWithWarning()
        {
            foreach (var missing in new[] { null, "", "   " })
            {
                var logger = new ListLogger();
                var hasher = new SecureHasher(Options.Create(new AppSettings { TokenSigningKey = missing }), logger);

                Assert.IsFalse(hasher.HasTokenSigningKey);
                Assert.IsTrue(logger.Entries.Any(x => x.Level == LogLevel.Warning && x.Message.Contains("TokenSigningKey")));

                // Same hash as before the change, so tokens handed out by the old server keep working.
                var token = new AuthToken { UserId = Guid.NewGuid(), IssuedUtc = DateTime.UtcNow, ExpiresUtc = DateTime.UtcNow.AddDays(180) };
                var oldHash = Sha256Hex(token.UserId + "|" + token.ExpiresUtc.ToString("g") + "|" + token.IssuedUtc.ToString("g") + "|" + OldConstant);
                Assert.AreEqual(oldHash, hasher.Get(token));
                token.Token = oldHash;
                Assert.IsTrue(hasher.IsValid(token));
            }

            var keyedLogger = new ListLogger();
            new SecureHasher(Options.Create(new AppSettings { TokenSigningKey = TestKey }), keyedLogger);
            Assert.AreEqual(0, keyedLogger.Entries.Count);
        }

        [TestMethod]
        public void PasswordHash_IsUnchangedByTheSigningKey()
        {
            var expected = Sha256Hex("hunter2|" + OldConstant);

            Assert.AreEqual(expected, new SecureHasher((string)null).Get("hunter2"));
            Assert.AreEqual(expected, new SecureHasher(TestKey).Get("hunter2"));
        }

        [TestMethod]
        public void SessionToken_ForOwnSession_IsAccepted()
        {
            var hasher = new SecureHasher(TestKey);
            var owner = Guid.NewGuid();
            var session = CreateSession(owner);
            var token = CreateSessionToken(hasher, owner, session.Id);

            Assert.IsTrue(SessionManager.IsValidSessionToken(token, session, hasher, out var error), error);
        }

        [TestMethod]
        public void SessionToken_ForAnotherUsersSession_IsRejected()
        {
            var hasher = new SecureHasher(TestKey);
            var owner = Guid.NewGuid();
            var attacker = Guid.NewGuid();
            var session = CreateSession(owner);

            // Even a correctly signed token for the attacker's own user id cannot point at the owner's session.
            var signedForAttacker = CreateSessionToken(hasher, attacker, session.Id);
            Assert.IsFalse(SessionManager.IsValidSessionToken(signedForAttacker, session, hasher, out _));

            // Nor can a token claiming to be the owner without the owner's signature.
            var forgedOwner = CreateSessionToken(hasher, attacker, session.Id);
            forgedOwner.UserId = owner;
            Assert.IsFalse(SessionManager.IsValidSessionToken(forgedOwner, session, hasher, out _));

            // Without a key the signature cannot be trusted, but the owner check still applies.
            var legacy = new SecureHasher((string)null);
            var legacyToken = CreateSessionToken(legacy, attacker, session.Id);
            Assert.IsFalse(SessionManager.IsValidSessionToken(legacyToken, session, legacy, out _));
        }

        [TestMethod]
        public void SessionToken_Tampered_IsRejected()
        {
            var hasher = new SecureHasher(TestKey);
            var owner = Guid.NewGuid();
            var session = CreateSession(owner);

            var otherSession = CreateSessionToken(hasher, owner, session.Id);
            otherSession.SessionId = Guid.NewGuid();
            Assert.IsFalse(SessionManager.IsValidSessionToken(otherSession, CreateSession(owner, otherSession.SessionId), hasher, out _));

            var oldStyle = CreateSessionToken(hasher, owner, session.Id);
            oldStyle.AuthToken = CreateAuthToken(new SecureHasher((string)null), owner).Token;
            Assert.IsFalse(SessionManager.IsValidSessionToken(oldStyle, session, hasher, out _));

            var empty = CreateSessionToken(hasher, owner, session.Id);
            empty.AuthToken = null;
            Assert.IsFalse(SessionManager.IsValidSessionToken(empty, session, hasher, out _));

            var valid = CreateSessionToken(hasher, owner, session.Id);
            Assert.IsFalse(SessionManager.IsValidSessionToken(valid, null, hasher, out _));
        }

        [TestMethod]
        public void SessionToken_FromOlderServer_IsAcceptedOnlyWithoutKey()
        {
            var owner = Guid.NewGuid();
            var session = CreateSession(owner);

            // Before this change the field held the auth token hash, which cannot be checked here.
            var token = new SessionToken
            {
                UserId = owner,
                SessionId = session.Id,
                ExpiresUtc = DateTime.UtcNow.AddDays(180),
                AuthToken = CreateAuthToken(new SecureHasher((string)null), owner).Token
            };

            Assert.IsTrue(SessionManager.IsValidSessionToken(token, session, new SecureHasher((string)null), out var error), error);
            Assert.IsFalse(SessionManager.IsValidSessionToken(token, session, new SecureHasher(TestKey), out _));
        }

        private static AuthToken CreateAuthToken(ISecureHasher hasher, Guid userId)
        {
            var token = new AuthToken
            {
                UserId = userId,
                IssuedUtc = DateTime.UtcNow,
                ExpiresUtc = DateTime.UtcNow + TimeSpan.FromDays(180)
            };
            token.Token = hasher.Get(token);
            return token;
        }

        private static SessionToken CreateSessionToken(ISecureHasher hasher, Guid userId, Guid sessionId)
        {
            return new SessionToken
            {
                UserId = userId,
                SessionId = sessionId,
                StartedUtc = DateTime.UtcNow,
                ExpiresUtc = DateTime.UtcNow + TimeSpan.FromDays(180),
                AuthToken = hasher.GetSessionSignature(userId, sessionId)
            };
        }

        private static GameSession CreateSession(Guid owner, Guid? id = null)
        {
            return new GameSession { Id = id ?? Guid.NewGuid(), UserId = owner };
        }

        private static AuthToken Copy(AuthToken token)
        {
            return new AuthToken { UserId = token.UserId, IssuedUtc = token.IssuedUtc, ExpiresUtc = token.ExpiresUtc, Token = token.Token };
        }

        private static string Sha256Hex(string text)
        {
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(text)).Select(x => x.ToString("x2")));
        }

        private class ListLogger : ILogger<SecureHasher>
        {
            public List<(LogLevel Level, string Message)> Entries { get; } = new();

            public IDisposable BeginScope<TState>(TState state) => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                Entries.Add((logLevel, formatter(state, exception)));
            }
        }
    }
}
