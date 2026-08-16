using Microsoft.AspNetCore.Components;
using RavenNest.Blazor.Services;
using RavenNest.BusinessLogic.Extended;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Components
{
    public partial class AdminUserView : ComponentBase
    {
        [Parameter]
        public WebsiteAdminUser SelectedUser { get; set; }
        private string editUserRemarkComment { get; set; }
        private bool EditingUserRemark { get; set; }
        private int? targetPatreonTier { get; set; }
        [Inject]
        UserService UserService { get; set; }
        [Inject]
        ClanService ClanService { get; set; }
        [Inject]
        LogoService LogoService { get; set; }
        private bool EditingUserPatreon { get; set; }
        private bool reloadingClanLogo { get; set; }

        private string[] patreonNames { get; set; } = new string[] {
            "None", "Mithril", "Rune", "Dragon", "Abraxas", "Phantom", "Above Phantom"
        };

        private int CharacterCount => SelectedUser?.Characters?.Count ?? 0;

        private int ConnectionCount => SelectedUser?.Connections?.Count ?? 0;

        private long StashCount => SelectedUser?.Stash?.Sum(x => x.Amount) ?? 0;

        private string PatreonName
        {
            get
            {
                var tier = SelectedUser?.PatreonTier ?? 0;
                if (tier < 0) tier = 0;
                return tier >= patreonNames.Length ? patreonNames[patreonNames.Length - 1] : patreonNames[tier];
            }
        }

        /// <summary>
        ///     There are three account statuses and the view could only ever say two things, one of
        ///     them by leaving the word out.
        /// </summary>
        private string StatusName => SelectedUser == null
            ? "Unknown"
            : ((BusinessLogic.Data.AccountStatus)SelectedUser.Status) switch
            {
                BusinessLogic.Data.AccountStatus.OK => "Active",
                BusinessLogic.Data.AccountStatus.TemporarilySuspended => "Temporarily suspended",
                BusinessLogic.Data.AccountStatus.PermanentlySuspended => "Permanently suspended",
                _ => "Status " + SelectedUser.Status
            };

        /// <summary>
        ///     Nobody counts back from a timestamp, which is the same reason the clan pages stopped
        ///     printing raw dates for a join date.
        /// </summary>
        private string AccountAge
        {
            get
            {
                if (SelectedUser == null) return null;

                var days = (int)(DateTime.UtcNow - SelectedUser.Created).TotalDays;
                if (days < 1) return "today";
                if (days == 1) return "1 day old";
                if (days < 60) return days + " days old";
                if (days < 730) return (days / 30) + " months old";
                return (days / 365) + " years old";
            }
        }

        private void IsHiddenInHighscoreChanged(object newValue)
        {
            var boolValue = newValue != null && newValue is bool b ? b : false;
            UserService.SetUserHiddenInHighscore(SelectedUser.Id, boolValue);
            SelectedUser.IsHiddenInHighscore = boolValue;
            InvokeAsync(StateHasChanged);
        }

        private void SelectedPatreonChanged(ChangeEventArgs e)
        {
            var id = e.Value?.ToString();
            if (int.TryParse(id, out var tier))
                targetPatreonTier = tier;
        }

        private void ResetClanNameChangeCounter()
        {
            if (ClanService.ResetNameChangeCounter(SelectedUser.Clan.Id))
            {
                SelectedUser.Clan.CanChangeName = true;
                SelectedUser.Clan.NameChangeCount = 0;
                InvokeAsync(StateHasChanged);
            }
        }

        private void EditRemark()
        {
            EditingUserRemark = true;
            editUserRemarkComment = SelectedUser.Comment;
        }

        private void CancelEditRemark()
        {
            EditingUserRemark = false;
        }

        private async void ApplyUserRemark()
        {
            if (EditingUserRemark)
            {
                await UserService.UpdateUserRemarkAsync(SelectedUser.Id, editUserRemarkComment);
                SelectedUser.Comment = editUserRemarkComment;
            }
            EditingUserRemark = false;
            await InvokeAsync(StateHasChanged);
        }

        private void EditPatreon()
        {
            EditingUserPatreon = true;
            targetPatreonTier = SelectedUser.PatreonTier ?? 0;
        }

        private async void UpdateUserPatreon()
        {
            if (targetPatreonTier == null )
            {
                return;
            }
            var patreonTier = targetPatreonTier.Value;
            await UserService.UpdateUserPatreonAsync(SelectedUser.Id, patreonTier);

            SelectedUser.PatreonTier = patreonTier;

            EditingUserPatreon = false;
            await InvokeAsync(StateHasChanged);

            //await LoadUserPageAsync(pageIndex, pageSize);
        }
        private void CancelEditUserPatreon()
        {
            EditingUserPatreon = false;
        }
    }
}
