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

        /// <summary>The clan takes members on request: a request was filed (or one was already waiting) instead of joining.</summary>
        public bool RequestSent { get; set; }

        /// <summary>The clan's name when the join did not happen, for the reply (the clan itself is only set on a join).</summary>
        public string ClanName { get; set; }

        /// <summary>Why the join did not happen, when there is more to say than that it failed.</summary>
        public JoinClanFailure Failure { get; set; }
    }

    public enum JoinClanFailure
    {
        None = 0,
        NoSuchClan = 1,
        AlreadyInClan = 2,
        InviteOnly = 3,
    }

    /// <summary>Who may join a clan (Clan.JoinPolicy).</summary>
    public enum ClanJoinPolicy
    {
        /// <summary>Anyone, with !clan join or from the clan's page.</summary>
        Open = 0,
        /// <summary>A request the clan's ranks that may invite accept or decline.</summary>
        OnRequest = 1,
        /// <summary>Only with an invite.</summary>
        InviteOnly = 2,
    }
}
