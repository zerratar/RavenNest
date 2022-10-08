using Microsoft.AspNetCore.Components;
using RavenNest.Blazor.Services;
using RavenNest.BusinessLogic.Extended;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Components
{
    public partial class AdminCharactersView : ComponentBase
    {

        [Inject]
        PlayerService CharacterService { get; set; }
        [Inject]
        AuthService AuthService { get; set; }
        [Parameter]
        public WebsiteAdminUser SelectedUser { get; set; }
        public List<WebsiteAdminPlayer> Characters { get; set; }

        [Parameter]
        public bool CanManage { get; set; }
        private Sessions.SessionInfo Session { get; set; }
        private bool CanModify { get => Session != null && Session.Administrator; }
        private bool ModifySkillDialogVisible { get; set; }
        private int ModifyingSkillLevel { get; set; } = 0;
        private int ModifyingSkillExperiencePercent { get; set; } = 0;
        private WebsiteAdminPlayer ModifyingCharacter { get; set; }

        private PlayerSkill ModifyingSkill;

        protected override void OnInitialized()
        {
            Session = AuthService.GetSession();
        }
        protected override Task OnParametersSetAsync()
        {
            Characters = SelectedUser.Characters;

            return base.OnParametersSetAsync();
        }
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);
        }

        public async void CloneSkillsAndStateToMain(WebsiteAdminPlayer character)
        {
            var result = await CharacterService.CloneSkillsAndStateToMainAsync(character.Id);

            if (result)
            {
                await InvokeAsync(StateHasChanged);
            }
        }

        public async void Unstuck(WebsiteAdminPlayer character)
        {
            await CharacterService.UnstuckPlayerAsync(character.Id);
        }
        public async void ResetSkills(WebsiteAdminPlayer character)
        {
            var result = await CharacterService.ResetPlayerSkillsAsync(character.Id);

            if (result)
            {
                //this.Player = await PlayerService.GetPlayerAsync(player.Id);
                await InvokeAsync(StateHasChanged);
            }
        }
        public string GetLastUpdateString(DateTime update)
        {
            var elapsed = DateTime.UtcNow - update;
            if (update == DateTime.MinValue)
            {
                return "";
            }
            var prefix = "Exp Last updated: ";
            if (elapsed.TotalHours >= 24)
            {
                return prefix + (int)elapsed.TotalDays + " days ago";
            }

            if (elapsed.TotalHours >= 1)
            {
                return prefix + (int)elapsed.TotalHours + " hours ago";
            }

            if (elapsed.TotalMinutes >= 1)
            {
                return prefix + (int)elapsed.TotalMinutes + " minutes ago";
            }

            return prefix + (int)elapsed.TotalSeconds + " seconds ago";
        }
        private string GetRestedTime(WebsiteAdminPlayer character)
        {
            return FormatTime(System.TimeSpan.FromSeconds(character.State.RestedTime));
        }
        private string FormatTime(TimeSpan time)
        {
            if (time.TotalSeconds < 60) return time.TotalSeconds + " seconds";
            if (time.TotalMinutes < 60)
                return (int)Math.Floor(time.TotalMinutes) + " minutes";

            return $"{time.Hours} hours, {time.Minutes} minutes";
        }
        private string GetTrainingSkillName(WebsiteAdminPlayer character)
        {
            var trainingSkill = character.TrainingSkill;
            if (string.IsNullOrEmpty(trainingSkill))
                return null;

            if (trainingSkill.Equals("all", StringComparison.OrdinalIgnoreCase))
                return "All";

            if (trainingSkill.Equals("heal", StringComparison.OrdinalIgnoreCase))
                return "Healing";

            if (character == null || character.Skills == null)
                return null;

            var training = character.Skills.AsList().FirstOrDefault(x => IsTrainingSkill(x, trainingSkill));
            return training?.Name;
        }

        private bool IsTrainingSkill(PlayerSkill skill, String trainingSkill)
        {
            if (string.IsNullOrEmpty(trainingSkill))
                return false;

            if (skill.Name.Equals(trainingSkill, StringComparison.OrdinalIgnoreCase))
                return true;

            if (trainingSkill == "heal")
                return skill.Name.Equals("healing", StringComparison.OrdinalIgnoreCase);

            if (trainingSkill.ToLower() == "all")
                return skill.Name.Equals("attack", StringComparison.OrdinalIgnoreCase) ||
                                skill.Name.Equals("defense", StringComparison.OrdinalIgnoreCase) ||
                                skill.Name.Equals("strength", StringComparison.OrdinalIgnoreCase);

            if (skill.Name.ToLower() == "attack" && trainingSkill.ToLower() == "atk")
                return true;

            if (trainingSkill.ToLower() == "mine" && skill.Name.Equals("mining", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }
        private string ExpDisplay(double value)
        {
            return value + " exp";
        }
        private string StyleWidth(int value)
        {
            return $"width: {value}px";
        }

        public async void ApplyModifySkill()
        {

            var result = await CharacterService.UpdatePlayerSkillAsync(ModifyingCharacter.Id, ModifyingSkill.Name, ModifyingSkillLevel, ModifyingSkillExperiencePercent / 100f);

            HideModifySkill();

            if (result)
            {
                //this.player = await PlayerService.GetPlayerAsync(player.Id);
                await InvokeAsync(StateHasChanged);
            }
        }

        public void HideModifySkill()
        {
            ModifySkillDialogVisible = false;
            ModifyingSkill = null;
            ModifyingCharacter = null;
        }

        public void ShowModifySkill(WebsiteAdminPlayer character, PlayerSkill skill)
        {
            ModifySkillDialogVisible = true;
            ModifyingSkill = skill;
            ModifyingSkillLevel = skill.Level;
            ModifyingSkillExperiencePercent = (int)(skill.Percent * 100);
            ModifyingCharacter = character;
        }
        private void OnLevelModified(ChangeEventArgs evt)
        {
            if (evt.Value != null && int.TryParse(evt.Value?.ToString() ?? ModifyingSkill.Level.ToString(), out var newLevel))
            {
                ModifyingSkillLevel = newLevel;
            }
        }

        private void OnExperienceModified(ChangeEventArgs evt)
        {
            if (evt.Value != null && int.TryParse(evt.Value?.ToString() ?? "0", out var newExpPercent))
            {
                ModifyingSkillExperiencePercent = newExpPercent;
            }
        }

        private int GetCurrentHealth(WebsiteAdminPlayer character)
        {
            if (character.State != null)
            {
                return character.State.Health;
            }
            else
            {
                return character.Skills.HealthLevel;
            }
        }
    }
}
