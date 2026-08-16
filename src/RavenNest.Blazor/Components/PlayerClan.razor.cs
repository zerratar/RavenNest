using Microsoft.AspNetCore.Components;
using RavenNest.Blazor.Services;
using RavenNest.BusinessLogic;
using RavenNest.BusinessLogic.Extended;
using RavenNest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Components
{
    public partial class PlayerClan
    {
        /// <summary>
        ///     How many members are listed before the roster asks to be expanded. Large clans
        ///     would otherwise push the clan skills and everything else off the screen.
        /// </summary>
        private const int CollapsedMemberCount = 12;

        private SessionInfo session;
        private Clan clan;
        private ClanRole clanRole;
        private IReadOnlyList<ClanMember> members;
        private Guid loadedMembersFor;
        private bool showAllMembers;
        private bool confirmingLeave;

        [Parameter]
        public WebsitePlayer Player { get; set; }

        [Parameter]
        public bool CanManage { get; set; }

        private bool IsOwner => clan != null && session != null && clan.OwnerUserId == session.UserId;

        private IReadOnlyList<ClanSkill> ClanSkills =>
            clan?.ClanSkills?.Where(x => x != null).ToList() ?? (IReadOnlyList<ClanSkill>)Array.Empty<ClanSkill>();

        /// <summary>
        ///     Clan experience counts progress within the current clan level, the same as a
        ///     character skill does, so the next level is what it is measured against.
        /// </summary>
        private double ClanLevelProgress
        {
            get
            {
                if (clan == null) return 0;
                var forNext = GameMath.ExperienceForLevel(clan.Level + 1);
                if (forNext <= 0) return 0;
                return Math.Clamp(clan.Experience / forNext, 0d, 1d);
            }
        }

        private double ClanExpToNextLevel =>
            clan == null ? 0 : Math.Max(0, GameMath.ExperienceForLevel(clan.Level + 1) - clan.Experience);

        // Whoever is playing right now first, then rank, then the strongest characters. A roster
        // is read to find someone to play with, so the ones actually playing lead.
        private IEnumerable<ClanMember> RankedMembers =>
            members == null
                ? Enumerable.Empty<ClanMember>()
                : members
                    .OrderByDescending(x => x.IsPlaying)
                    .ThenBy(x => x.InvitationPending ? 1 : 0)
                    .ThenByDescending(x => x.Player?.ClanRole?.Level ?? 0)
                    .ThenByDescending(x => CombatLevelOf(x.Player));

        private IEnumerable<ClanMember> VisibleMembers =>
            showAllMembers ? RankedMembers : RankedMembers.Take(CollapsedMemberCount);

        private int HiddenMemberCount => Math.Max(0, (members?.Count ?? 0) - CollapsedMemberCount);

        private static int CombatLevelOf(Player player)
        {
            var skills = player?.Skills;
            if (skills == null) return 3;

            return (int)(((skills.AttackLevel + skills.DefenseLevel + skills.HealthLevel + skills.StrengthLevel) / 4f)
                + ((skills.RangedLevel + skills.MagicLevel + skills.HealingLevel) / 8f));
        }

        private static double SkillProgressOf(ClanSkill skill)
        {
            var forNext = GameMath.ExperienceForLevel(skill.Level + 1);
            if (forNext <= 0) return 0;
            return Math.Clamp(skill.Experience / forNext, 0d, 1d);
        }

        protected override async Task OnInitializedAsync()
        {
            session = AuthService.GetSession();
            if (session.UserName != null)
            {
                clan = Player.Clan;
                clanRole = Player.ClanRole;
                LoadMembers();
                await InvokeAsync(StateHasChanged);
            }
        }

        protected override void OnParametersSet()
        {
            if (Player != null)
            {
                clan = Player.Clan;
                clanRole = Player.ClanRole;
                LoadMembers();
            }
        }

        /// <summary>
        ///     Guarded by the clan it was loaded for, because OnParametersSet runs on every render
        ///     of the parent and this reaches the data layer.
        /// </summary>
        private void LoadMembers()
        {
            if (clan == null || session == null || !session.Authenticated || loadedMembersFor == clan.Id)
                return;

            loadedMembersFor = clan.Id;
            showAllMembers = false;

            try
            {
                members = ClanService.GetMembers(clan.Id);
            }
            catch
            {
                // An unreadable roster is not a reason to lose the rest of the tab.
                members = null;
            }
        }

        private void ShowAllMembers()
        {
            showAllMembers = true;
        }

        private void StartLeave()
        {
            confirmingLeave = true;
        }

        private void CancelLeave()
        {
            confirmingLeave = false;
        }

        /// <summary>
        ///     Leaving used to happen on the first click with no way back. Clan membership is not
        ///     something you can restore yourself, so it asks first.
        /// </summary>
        private Task LeaveClanAsync()
        {
            if (clan == null || Player == null)
                return Task.CompletedTask;

            confirmingLeave = false;

            return Task.Run(() =>
            {
                ClanService.RemoveMember(clan.Id, Player.Id);
                InvokeAsync(() => NavigationManager.NavigateTo("/characters", true));
            });
        }
    }
}
