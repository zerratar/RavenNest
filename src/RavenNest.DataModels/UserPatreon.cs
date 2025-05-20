using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    public partial class UserPatreon : Entity<UserPatreon>
    {
        [PersistentData] private Guid? userId;
        [PersistentData] private string twitchUserId;
        [PersistentData] private string pledgeTitle;
        [PersistentData] private string firstName;
        [PersistentData] private string fullName;
        [PersistentData] private long? patreonId;
        [PersistentData] private long? pledgeAmount;
        [PersistentData] private string email;
        [PersistentData] private int? tier;
        [PersistentData] private string accessToken;
        [PersistentData] private string refreshToken;
        [PersistentData] private string profilePicture;
        [PersistentData] private string expiresIn;
        [PersistentData] private string scope;
        [PersistentData] private string tokenType;
        [PersistentData] private DateTime? updated;
        [PersistentData] private DateTime? created;
    }
}
