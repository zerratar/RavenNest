using Microsoft.AspNetCore.Components;
using RavenNest.BusinessLogic.Extended;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Pages.Admin
{
    public partial class UserSearch : ComponentBase
    {
        [Inject]
        Services.AuthService AuthService { get; set; }
        [Inject]
        Services.UserService UserService { get; set; }
        [Inject]
        Services.ClanService ClanService { get; set; }
        public bool loading { get; set; } = false;
        private PlayerSearchModel searchModel { get; set; } = new PlayerSearchModel();
        private SessionInfo session { get; set; }
        private IReadOnlyList<WebsiteAdminUser> users { get; set; }
        private int pageSize { get; set; } = 25;
        private long totalCount { get; set; } = 0;
        private string[] patreonNames { get; set; } = new string[] {
            "None", "Mithril", "Rune", "Dragon", "Abraxas", "Phantom", "Above Phantom"
        };

        protected override void OnInitialized()
        {
            session = AuthService.GetSession();
        }
        private void Filter()
        {
            LoadUserPageAsync(pageSize);
        }
        private async Task LoadUserPageAsync(int take)
        {
            loading = true;
            var filter = searchModel.Query;
            var result = await UserService.SearchForUserByUserOrPlayersLimitedAsync(filter, take);
            users = result;
            totalCount = result.Count;
            loading = false;
            await InvokeAsync(StateHasChanged);
        }
    }
    public class PlayerSearchModel
    {
        public string Query { get; set; }
    }
}
