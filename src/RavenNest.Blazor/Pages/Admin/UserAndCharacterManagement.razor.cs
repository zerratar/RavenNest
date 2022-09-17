using Microsoft.AspNetCore.Components;
using System.Threading.Tasks;
using RavenNest.BusinessLogic.Extended;
using System;
using System.Linq;

namespace RavenNest.Blazor.Pages.Admin
{
    public enum CharacterViewState
    {
        Skills,
        Inventory,
        Clan
    }

    public partial class UserAndCharacterManagement : ComponentBase
    {
        [Inject]
        Services.AuthService AuthService { get; set; }
        [Inject]
        Services.UserService UserService { get; set; }
        [Inject]
        NavigationManager NavigationManager { get; set; }

        [Parameter]
        public string Id { get; set; }
        [Parameter]
        public int? View { get; set; }
        private CharacterViewState ViewState { get; set; }


        private WebsiteAdminUser SelectedUser { get; set; }
        private Sessions.SessionInfo Session { get; set; }


        protected override Task OnInitializedAsync()
        {
            Session = AuthService.GetSession();

            if (Id != null)
            {
                SelectedUser = UserService.GetUser(Id);
            }

            return base.OnInitializedAsync();
        }

        protected override void OnParametersSet()
        {
            //Memorize valid ViewState so when page is refreshed, we can stay on the same page
            View = View ?? 0;
            if (Enum.IsDefined(typeof(CharacterViewState), View))
            {
                ViewState = (CharacterViewState)View;
            }
            else
            {
                View = 0;
                ViewState = (CharacterViewState)View;
                ViewStateNavigation(CharacterViewState.Skills);
            }

            base.OnParametersSet();
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
            ViewStateNavigation(ViewState);
        }

        private void ShowSkills()
        {
            ViewState = CharacterViewState.Skills;
            ViewStateNavigation(ViewState);
        }

        private void ShowClan()
        {
            ViewState = CharacterViewState.Clan;
            ViewStateNavigation(ViewState);
        }

        private void ViewStateNavigation(CharacterViewState view)
        {
            var relativeNavURL = "/admin/user/" + Id;
            if (view > 0)
                relativeNavURL.Concat("/" + ((int)view).ToString());
            NavigationManager.NavigateTo(relativeNavURL);
        }
        private string SelectedClassStyling(CharacterViewState state)
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
