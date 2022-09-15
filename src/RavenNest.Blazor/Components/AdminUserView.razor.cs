using Microsoft.AspNetCore.Components;
using RavenNest.Blazor.Pages.Front;
using RavenNest.Blazor.Services;
using RavenNest.BusinessLogic.Extended;
using RavenNest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static RavenNest.Blazor.Components.AdminCharactersView;

namespace RavenNest.Blazor.Components
{
    public partial class AdminUserView : ComponentBase
    {
        [Parameter]
        public WebsiteAdminUser SelectedUser { get; set; }
        [Parameter]
        public CharacterViewState ViewState { get; set; }
        private string editUserRemarkComment { get; set; }
        private bool EditingUserRemark { get; set; }
        private int? targetPatreonTier { get; set; }
        [Inject]
        UserService UserService { get; set; }
        [Inject]
        ClanService ClanService { get; set; }
        [Inject]
        LogoService LogoService { get; set; }
        [Inject] 
        RavenNest.Blazor.Services.ItemService ItemService { get; set; }
        private bool EditingUserPatreon { get; set; }
        private bool reloadingClanLogo { get; set; }

        private IReadOnlyList<UserBankItem> _stash;

        private IReadOnlyList<UserBankItem> Stash
        {
            get { return _stash ?? new List<RavenNest.Models.UserBankItem>(); }
            set { _stash = value; }
        }
        private Dictionary<Guid, RavenNest.Models.Item> itemLookup;

        protected override Task OnParametersSetAsync()
        {
            var stash = SelectedUser.Stash;
            if (stash != null)
            {
                Stash = stash;
                this.itemLookup = stash.Select(x => x.ItemId).Distinct().Select(ItemService.GetItem).ToDictionary(x => x.Id, x => x);
            }
            return base.OnParametersSetAsync();
        }

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
            if (targetPatreonTier == null)
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
        public string GetItemImage(Guid itemId, string tag)
        {
            if (tag != null)
            {
                return $"/api/twitch/logo/{tag}";
            }
            return $"/imgs/items/{itemId}.png";
        }

        public string GetItemAmount(long amount)
        {
            var value = amount;
            if (value >= 1000_000)
            {
                var mils = value / 1000000.0;
                return Math.Round(mils) + "M";
            }
            else if (value > 1000)
            {
                var ks = value / 1000m;
                return Math.Round(ks) + "K";
            }

            return amount.ToString();
        }
    }
}
