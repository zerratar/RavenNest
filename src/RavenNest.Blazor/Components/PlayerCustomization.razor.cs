﻿using Microsoft.AspNetCore.Components;
using RavenNest.BusinessLogic.Extended;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Components
{
    public partial class PlayerCustomization
    {
        private Models.SessionInfo session;

        [Parameter]
        public WebsitePlayer Player { get; set; }

        [Parameter]
        public bool CanManage { get; set; }

        protected override void OnInitialized()
        {
            session = AuthService.GetSession();
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (session == null || session.UserName == null)
            {
                NavigationManager.NavigateTo("/login");
                return;
            }

            if (session != null && session.UserName != null)
            {
                await JS.InvokeAsync<object>("showCharacterCustomization", System.Array.Empty<object>());
            }
        }
    }
}
