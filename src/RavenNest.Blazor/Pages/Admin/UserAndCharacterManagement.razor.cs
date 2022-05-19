using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RavenNest.BusinessLogic.Extended;
using RavenNest.Models;

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


        private WebsiteAdminUser selectedUser { get; set; }
        private BusinessLogic.Extended.WebsiteAdminPlayer selectedPlayer { get; set; }
        private RavenNest.Sessions.SessionInfo session { get; set; }

        protected override async Task OnInitializedAsync()
        {
            session = AuthService.GetSession();

            if (Id != null)
            {
                selectedUser = UserService.GetUser(Id);
            }
            if (selectedUser == null || session == null)
                return;
            selectedPlayer = selectedUser.Characters.FirstOrDefault();
        }

        protected override void OnAfterRender(bool firstRender)
        {
            if (session == null || session.UserId == null && selectedUser == null)
            {
                NavigationManager.NavigateTo("/login");
            }
        }

        private async void SelectPlayer(BusinessLogic.Extended.WebsiteAdminPlayer player)
        {
            selectedPlayer = selectedUser.Characters.FirstOrDefault(c=> c.Id == player.Id);
            await InvokeAsync(StateHasChanged);
        }

    }
}
