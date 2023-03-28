using Microsoft.AspNetCore.Components;
using RavenNest.BusinessLogic.Extended;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Components
{
    public partial class PlayerMap
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
                var x = (double)Player.State.X.GetValueOrDefault();
                var y = (double)Player.State.Y.GetValueOrDefault();
                var z = (double)Player.State.Z.GetValueOrDefault();

                await JS.InvokeAsync<object>("initWorldMap", new object[] { x, y, z });
            }
        }
    }
}
