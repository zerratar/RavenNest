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

        private void IsHiddenInHighscoreChanged(object? newValue)
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

        private async Task BanUser()
        {
            if (await UserService.SetUserStatusAsync(SelectedUser.Id, BusinessLogic.Data.AccountStatus.PermanentlySuspended))
            {
                SelectedUser.Status = 2;
                await InvokeAsync(StateHasChanged);
            }
        }

        private async Task UnbanUser()
        {
            if (await UserService.SetUserStatusAsync(SelectedUser.Id, BusinessLogic.Data.AccountStatus.OK))
            {
                SelectedUser.Status = 0;
                await InvokeAsync(StateHasChanged);
            }
        }
        private async void ReloadClanLogo()
        {
            reloadingClanLogo = true;
            await InvokeAsync(StateHasChanged);

            if (SelectedUser.HasClan)
            {
                await LogoService.UpdateClanLogoAsync(SelectedUser.UserId);

                reloadingClanLogo = false;
                await InvokeAsync(StateHasChanged);
            }
        }
    }
}
