using System;
using System.Collections.Generic;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Extended
{
    public class WebsiteAdminUser
    {
        public Guid Id { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
        public int? PatreonTier { get; set; }
        public string Email { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsModerator { get; set; }
        public List<WebsiteAdminPlayer> Characters { get; set; }
        public int Status { get; set; }
        public bool IsHiddenInHighscore { get; set; }
        public DateTime Created { get; set; }
        public bool HasClan { get; set; }
        public WebsiteClan Clan { get; set; }
        public List<UserBankItem> Stash { get; set; }
        public string Comment { get; set; }
    }

    public class WebsiteClan
    {
        public Guid Id { get; set; }
        public int Level { get; set; }
        public int NameChangeCount { get; set; }
        public bool CanChangeName { get; set; }
    }
}
