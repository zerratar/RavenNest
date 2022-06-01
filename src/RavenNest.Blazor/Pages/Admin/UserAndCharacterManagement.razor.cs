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
    }
}
