using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RavenNest.BusinessLogic.Extended;
using RavenNest.Models;
using static RavenNest.Blazor.Components.AdminCharactersView;

namespace RavenNest.Blazor.Pages.Admin
{
    public partial class UserAndCharacterManagement : ComponentBase
    {
        [Inject]
        Services.AuthService AuthService { get; set; }
        [Inject]
        Services.UserService UserService { get; set; }
        [Inject]
        Services.ClanService ClanService { get; set; }
        [Inject]
        NavigationManager NavigationManager { get; set; }
        [Inject]
        Services.PlayerService PlayerService { get; set; }

        [Parameter]
        public string Id { get; set; }
        [Parameter]
        public int? View { get; set; }
        private CharacterViewState ViewState { get; set; }


        private WebsiteAdminUser SelectedUser { get; set; }
        private Sessions.SessionInfo Session { get; set; }

        protected override async Task OnInitializedAsync()
        {
            Session = AuthService.GetSession();

            if (Id != null)
            {
                SelectedUser = UserService.GetUser(Id);
            }
        }

        protected override void OnAfterRender(bool firstRender)
        {
            if (Session == null || Session.UserId == null && SelectedUser == null)
            {
                NavigationManager.NavigateTo("/login");
            }
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

        private void ShowInventory()
        {
            ViewState = CharacterViewState.Inventory;
        }

        private void ShowSkills()
        {
            ViewState = CharacterViewState.Skills;
        }

        private void ShowClan()
        {
            ViewState = CharacterViewState.Clan;
        }

        private void ShowMap()
        {
            ViewState = CharacterViewState.Map;
        }

        private void ShowCustomization()
        {
            ViewState = CharacterViewState.Customization;
        }
        private string SelectedClass(CharacterViewState state)
        {
            return ViewState == state ? "active" : "";
        }
        /*        protected override void OnParametersSet()
                {
                    if (SelectedPlayer != null && CanManage)
                    {
                        PlayerService.SetActiveCharacter(SelectedPlayer);
                    }
                }*/

    }
}
