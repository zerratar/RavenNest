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
        private Models.SessionInfo session { get; set; }
        private IReadOnlyList<WebsiteAdminUser> users { get; set; }
        private int pageSize { get; set; } = 25;
        private string[] patreonNames { get; set; } = new string[] {
            "None", "Mithril", "Rune", "Dragon", "Abraxas", "Phantom", "Above Phantom"
        };

        private string PatreonName(int? tier)
        {
            var t = tier ?? 0;
            if (t < 0) t = 0;
            return t >= patreonNames.Length ? patreonNames[patreonNames.Length - 1] : patreonNames[t];
        }

        private static string StatusName(int status)
        {
            return ((BusinessLogic.Data.AccountStatus)status) switch
            {
                BusinessLogic.Data.AccountStatus.OK => "Active",
                BusinessLogic.Data.AccountStatus.TemporarilySuspended => "Temporarily suspended",
                BusinessLogic.Data.AccountStatus.PermanentlySuspended => "Permanently suspended",
                _ => "Status " + status
            };
        }

        /// <summary>
        ///     Which stream the character is locked to, which is what most support questions turn
        ///     out to be about. It was already on the row, rendered as bare text with no label.
        /// </summary>
        private static string SessionNote(WebsiteAdminPlayer character)
        {
            return string.IsNullOrEmpty(character.SessionName)
                ? "Not on a stream"
                : "Playing on " + character.SessionName;
        }

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
            loading = false;
            await InvokeAsync(StateHasChanged);
        }
    }
    public class PlayerSearchModel
    {
        public string Query { get; set; }
    }
}
