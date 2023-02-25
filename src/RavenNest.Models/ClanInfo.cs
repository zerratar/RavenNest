using System;
using System.Collections.Generic;

namespace RavenNest.Models
{
    public class ClanInfo
    {
        public string Name { get; set; }
        public string OwnerName { get; set; }
        public List<ClanRoleInfo> Roles { get; set; }
    }

    public class ClanRoleInfo
    {
        public string Name { get; set; }
        public int MemberCount { get; set; }
        public int Level { get; set; }
    }

    public class ClanDeclineResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class ClanLeaveResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class ClanInviteResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }

        public static implicit operator ClanInviteResult(bool value)
        {
            return new ClanInviteResult
            {
                Success = value,
                ErrorMessage = value ? "Unknown error. Please try again later" : null
            };
        }

        public static implicit operator bool(ClanInviteResult value)
        {
            return value.Success;
        }
    }

    public class ChangeRoleResult
    {
        public bool Success { get; set; }
        public ClanRole NewRole { get; set; }
    }

    public class JoinClanResult
    {
        public bool Success { get; set; }
        public Clan Clan { get; set; }
        public ClanRole Role { get; set; }
        public string WelcomeMessage { get; set; }
    }
}
