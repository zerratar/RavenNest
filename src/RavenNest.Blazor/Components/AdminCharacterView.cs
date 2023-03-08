using Microsoft.AspNetCore.Components;
using RavenNest.BusinessLogic.Extended;

namespace RavenNest.Blazor.Components
{
    public partial class AdminCharacterView : ComponentBase
    {
        [Inject]
        Services.PlayerService PlayerService { get; set; }
        [Inject]
        Services.AuthService AuthService { get; set; }
        [Parameter]
        public WebsiteAdminPlayer SelectedPlayer { get; set; }

        [Parameter]
        public bool CanManage { get; set; }

        private SessionInfo session;
        private bool editingAlias;
        private string oldIdentifier;
        private CharacterViewState viewState;

        protected override void OnInitialized()
        {
            session = AuthService.GetSession();
            if (SelectedPlayer != null && CanManage)
            {
                PlayerService.SetActiveCharacter(SelectedPlayer);
            }
        }

        protected override void OnParametersSet()
        {
            if (SelectedPlayer != null && CanManage)
            {
                PlayerService.SetActiveCharacter(SelectedPlayer);
            }
        }

        private string SelectedClass(CharacterViewState state)
        {
            return viewState == state ? "active" : "";
        }

        private void EditAlias()
        {
            oldIdentifier = SelectedPlayer.Identifier;
            editingAlias = true;
        }

        private void CancelEditAlias()
        {
            SelectedPlayer.Identifier = oldIdentifier;
            editingAlias = false;
        }
        private void UpdateAlias()
        {
            PlayerService.UpdatePlayerIdentifier(SelectedPlayer.Id, SelectedPlayer.Identifier);
            editingAlias = false;
            InvokeAsync(StateHasChanged);
        }

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
