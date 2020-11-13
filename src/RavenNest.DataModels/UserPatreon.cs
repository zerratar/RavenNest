using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RavenNest.DataModels
{
    public class UserPatreon : Entity<UserPatreon>
    {
        private Guid id; public Guid Id { get => id; set => Set(ref id, value); }
        private Guid? userId; public Guid? UserId { get => userId; set => Set(ref userId, value); }
        private string twitchUserId; public string TwitchUserId { get => twitchUserId; set => Set(ref twitchUserId, value); }
        private string pledgeTitle; public string PledgeTitle { get => pledgeTitle; set => Set(ref pledgeTitle, value); }
        private string firstName; public string FirstName { get => firstName; set => Set(ref firstName, value); }
        private string fullName; public string FullName { get => fullName; set => Set(ref fullName, value); }
        private long? patreonId; public long? PatreonId { get => patreonId; set => Set(ref patreonId, value); }
        private long? pledgeAmount; public long? PledgeAmount { get => pledgeAmount; set => Set(ref pledgeAmount, value); }
        private string email; public string Email { get => email; set => Set(ref email, value); }
        private int? tier; public int? Tier { get => tier; set => Set(ref tier, value); }

    }
}
