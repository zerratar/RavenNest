using System;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    public interface ISecureHasher
    {
        /// <summary>
        /// True when AppSettings:TokenSigningKey is set and tokens are signed with it.
        /// False means tokens fall back to the old, publicly known constant.
        /// </summary>
        bool HasTokenSigningKey { get; }

        string Get(AuthToken token);
        string Get(SessionToken token);

        /// <summary>
        /// Password hashing. Unchanged on purpose: stored password hashes depend on it.
        /// </summary>
        string Get(string text);

        /// <summary>
        /// Checks the auth token's signature in constant time. Does not check expiry.
        /// </summary>
        bool IsValid(AuthToken token);

        /// <summary>
        /// The value placed in SessionToken.AuthToken. Binds the token to its user and session.
        /// </summary>
        string GetSessionSignature(Guid userId, Guid sessionId);

        /// <summary>
        /// Checks SessionToken.AuthToken against GetSessionSignature in constant time.
        /// </summary>
        bool IsValidSessionSignature(SessionToken token);
    }
}
