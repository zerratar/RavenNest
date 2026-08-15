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
        public Guid? Id { get; set; }
        private CharacterViewState ViewState { get; set; }

        private WebsiteAdminUser SelectedUser { get; set; }
        private SessionInfo Session { get; set; }

        private bool loading = true;
        private bool confirmingStatusChange;
        private bool liftingSuspension;

        protected override async Task OnInitializedAsync()
        {
            Session = AuthService.GetSession();

            if (Id != null)
            {
                SelectedUser = UserService.GetUser(Id.Value);
            }

            loading = false;
        }

        /// <summary>
        ///     An id that resolves to nothing used to bounce the administrator to the login page,
        ///     which reads as "you are not allowed in here" when the truth is "there is no such
        ///     user". Only the session check redirects now; the page says the rest itself.
        /// </summary>
        protected override void OnAfterRender(bool firstRender)
        {
            if (Session == null || !Session.Authenticated)
            {
                NavigationManager.NavigateTo("/login");
            }
        }

        private void Confirm(bool lift)
        {
            liftingSuspension = lift;
            confirmingStatusChange = true;
        }

        private void CancelStatusChange()
        {
            confirmingStatusChange = false;
        }

        /// <summary>
        ///     Suspending an account used to happen on the first click of a button that was held at
        ///     40% opacity until the pointer passed over it.
        /// </summary>
        private async Task ApplyStatusChange()
        {
            confirmingStatusChange = false;

            var target = liftingSuspension
                ? BusinessLogic.Data.AccountStatus.OK
                : BusinessLogic.Data.AccountStatus.PermanentlySuspended;

            if (await UserService.SetUserStatusAsync(SelectedUser.Id, target))
            {
                SelectedUser.Status = (int)target;
            }

            await InvokeAsync(StateHasChanged);
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
