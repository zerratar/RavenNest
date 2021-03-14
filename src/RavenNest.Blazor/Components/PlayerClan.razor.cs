using Microsoft.AspNetCore.Components;
using RavenNest.BusinessLogic.Extended;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Components
{
    public partial class PlayerClan
    {
        private RavenNest.Sessions.SessionInfo session;
        private RavenNest.Models.Clan clan;
        private RavenNest.Models.ClanRole clanRole;

        [Parameter]
        public WebsitePlayer Player { get; set; }

        [Parameter]
        public bool CanManage { get; set; }

        protected override async Task OnInitializedAsync()
        {
            session = AuthService.GetSession();
            if (session.UserId != null)
            {
                clan = Player.Clan;
                clanRole = Player.ClanRole;
                await InvokeAsync(StateHasChanged);
            }
        }

        protected override void OnAfterRender(bool firstRender)
        {
            base.OnAfterRender(firstRender);

            if (session.UserId == null)
            {
                NavigationManager.NavigateTo("/login");
                return;
            }
        }

        protected override void OnParametersSet()
        {
            if (Player != null)
            {
                clan = Player.Clan;
                clanRole = Player.ClanRole;
            }
        }
        private Task LeaveClanAsync()
        {
            if (clan == null || Player == null)
                return Task.CompletedTask;

            return Task.Run(() =>
            {
                ClanService.RemoveMember(clan.Id, Player.Id);
                InvokeAsync(() => NavigationManager.NavigateTo("/characters", true));
            });
        }
    }
}
