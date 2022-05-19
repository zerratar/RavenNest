using Microsoft.AspNetCore.Components;
using RavenNest.BusinessLogic.Extended;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Pages.Admin
{
    public partial class UserSearch : ComponentBase
    {
        [Inject]
        Services.AuthService AuthService { get; set; }
        [Inject]
        Services.UserService UserService { get; set; }
        [Inject]
        Services.ClanService ClanService { get; set; }
        public bool loading { get; set; } = false;
        private PlayerSearchModel searchModel { get; set; } = new PlayerSearchModel();
        private Sessions.SessionInfo session { get; set; }
        private IReadOnlyList<WebsiteAdminUser> users { get; set; }
        private int pageSize { get; set; } = 25;
        private long totalCount { get; set; } = 0;
        private string editUserRemarkComment { get; set; }
        private WebsiteAdminUser editUserRemarkUser { get; set; }
        private Guid? editingPatreonUserId { get; set; }
        private int? targetPatreonTier { get; set; }
        private string[] patreonNames { get; set; } = new string[] {
            "None", "Mithril", "Rune", "Dragon", "Abraxas", "Phantom", "Above Phantom"
        };

        protected override void OnInitialized()
        {
            session = AuthService.GetSession();
        }

        private void IsHiddenInHighscoreChanged(WebsiteAdminUser user, object? newValue)
        {
            var boolValue = newValue != null && newValue is bool b ? b : false;
            UserService.SetUserHiddenInHighscore(user.Id, boolValue);
            user.IsHiddenInHighscore = boolValue;
            InvokeAsync(StateHasChanged);
        }

        private void SelectedPatreonChanged(ChangeEventArgs e)
        {
            var id = e.Value?.ToString();
            if (int.TryParse(id, out var tier))
                targetPatreonTier = tier;
        }

        private void ResetClanNameChangeCounter(WebsiteAdminUser user)
        {
            if (ClanService.ResetNameChangeCounter(user.Clan.Id))
            {
                user.Clan.CanChangeName = true;
                user.Clan.NameChangeCount = 0;
                InvokeAsync(StateHasChanged);
            }
        }

        private void EditRemark(WebsiteAdminUser user)
        {
            editUserRemarkUser = user;
            editUserRemarkComment = user.Comment;
        }

        private void CancelEditRemark()
        {
            editUserRemarkUser = null;
        }

        private async void ApplyUserRemark()
        {
            if (editUserRemarkUser != null)
            {
                await UserService.UpdateUserRemarkAsync(editUserRemarkUser.Id, editUserRemarkComment);
                editUserRemarkUser.Comment = editUserRemarkComment;
            }
            editUserRemarkUser = null;
            await InvokeAsync(StateHasChanged);
        }

        private void EditPatreon(WebsiteAdminUser user)
        {
            editingPatreonUserId = user.Id;
            targetPatreonTier = user.PatreonTier ?? 0;
        }

        private void CancelEditUserPatreon()
        {
            editingPatreonUserId = null;
        }

        private async void UpdateUserPatreon()
        {
            if (targetPatreonTier == null || editingPatreonUserId == null)
            {
                return;
            }
            var userId = editingPatreonUserId.Value;
            var patreonTier = targetPatreonTier.Value;
            await UserService.UpdateUserPatreonAsync(userId, patreonTier);
            var user = users.FirstOrDefault(x => x.Id == userId);
            if (user != null)
            {
                user.PatreonTier = patreonTier;
            }
            CancelEditUserPatreon();
            await InvokeAsync(StateHasChanged);

            //await LoadUserPageAsync(pageIndex, pageSize);
        }

        private void Filter()
        {
            LoadUserPageAsync(pageSize);
        }

        private async Task LoadUserPageAsync(int take)
        {
            loading = true;
            var filter = searchModel.Query;
            var result = await UserService.SearchForUserByUserOrPlayersLimitedAsync(filter, take);
            users = result;
            totalCount = result.Count;
            loading = false;
            await InvokeAsync(StateHasChanged);
        }

        private async Task BanUser(WebsiteAdminUser user)
        {
            if (await UserService.SetUserStatusAsync(user.Id, BusinessLogic.Data.AccountStatus.PermanentlySuspended))
            {
                user.Status = 2;
                await InvokeAsync(StateHasChanged);
            }
        }

        private async Task UnbanUser(WebsiteAdminUser user)
        {
            if (await UserService.SetUserStatusAsync(user.Id, BusinessLogic.Data.AccountStatus.OK))
            {
                user.Status = 0;
                await InvokeAsync(StateHasChanged);
            }
        }
    }
    public class PlayerSearchModel
    {
        public string Query { get; set; }
    }
}
