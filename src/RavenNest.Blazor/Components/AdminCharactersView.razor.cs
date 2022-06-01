using Microsoft.AspNetCore.Components;
using RavenNest.BusinessLogic.Extended;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RavenNest.Blazor.Components
{
    public partial class AdminCharactersView : ComponentBase
    {
        [Inject]
        Services.PlayerService PlayerService { get; set; }
        [Inject]
        Services.AuthService AuthService { get; set; }
        [Parameter]
        public List<WebsiteAdminPlayer> Characters { get; set; }

        [Parameter]
        public bool CanManage { get; set; }

        private CharacterViewState viewState { get; set; }

        private void ShowInventory()
        {
            viewState = CharacterViewState.Inventory;
        }

        private void ShowSkills()
        {
            viewState = CharacterViewState.Skills;
        }

        private void ShowClan()
        {
            viewState = CharacterViewState.Clan;
        }

        private void ShowMap()
        {
            viewState = CharacterViewState.Map;
        }

        private void ShowCustomization()
        {
            viewState = CharacterViewState.Customization;
        }
        private string SelectedClass(CharacterViewState state)
        {
            return viewState == state ? "active" : "";
        }
/*        protected override void OnParametersSet()
        {
            if (SelectedPlayer != null && CanManage)
            {
                PlayerService.SetActiveCharacter(SelectedPlayer);
            }
        }*/

        private enum CharacterViewState
        {
            Skills,
            Inventory,
            Clan,
            Customization,
            Map
        }
    }
}
