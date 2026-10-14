using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    /// <summary>
    /// A game client allowed to sign in on its own: the computer a streamer approved, through
    /// Twitch on the website or by signing in with a password and ticking remember me. The client
    /// keeps a device token, the id of this row and a secret; only a hash of the secret is kept
    /// here, so a copy of the database cannot be used to sign in.
    /// </summary>
    public partial class ClientDevice : Entity<ClientDevice>
    {
        [PersistentData] private Guid userId;
        /// <summary>What the client called itself, usually the computer's name.</summary>
        [PersistentData] private string name;
        /// <summary>How it was approved: "twitch" or "password".</summary>
        [PersistentData] private string platform;
        /// <summary>SHA-256 of the secret half of the device token, in hex.</summary>
        [PersistentData] private string secretHash;
        [PersistentData] private DateTime created;
        [PersistentData] private DateTime lastUsed;
        /// <summary>Moves forward on each use; a device left unused this long has to be approved again.</summary>
        [PersistentData] private DateTime expires;
    }
}
