using System;
using System.Collections.Generic;

namespace RavenNest.DataModels
{
    public partial class User
    {
        public User()
        {
            CharacterOriginUser = new HashSet<Character>();
            CharacterUser = new HashSet<Character>();
            GameSession = new HashSet<GameSession>();
        }

        public Guid Id { get; set; }
        public string UserId { get; set; }        
        public string UserName { get; set; }
        public string DisplayName { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public DateTime Created { get; set; }

        public ICollection<Character> CharacterOriginUser { get; set; }
        public ICollection<Character> CharacterUser { get; set; }
        public ICollection<GameSession> GameSession { get; set; }

    }
}
