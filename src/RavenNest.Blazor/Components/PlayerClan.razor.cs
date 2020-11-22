using Microsoft.AspNetCore.Components;
using RavenNest.BusinessLogic.Extended;

namespace RavenNest.Blazor.Components
{
    public partial class PlayerClan
    {
        [Parameter]
        public WebsitePlayer Player { get; set; }

        [Parameter]
        public bool CanManage { get; set; }
    }
}
