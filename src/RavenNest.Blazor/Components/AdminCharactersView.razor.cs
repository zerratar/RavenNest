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

        [Parameter]
        public CharacterViewState ViewState { get; set; }
        private Sessions.SessionInfo session { get; set; }
        private bool CanModify { get => session != null && session.Administrator; }
        private string TrainingSkill { get; set; }


        public enum CharacterViewState
        {
            Skills,
            Inventory,
            Clan,
            Customization,
            Map
        }
        protected override void OnInitialized()
        {
            session = AuthService.GetSession();
        }

        public async void CloneSkillsAndStateToMain(WebsiteAdminPlayer player)
        {
            var result = await PlayerService.CloneSkillsAndStateToMainAsync(player.Id);

            if (result)
            {
                await InvokeAsync(StateHasChanged);
            }
        }

        public async void Unstuck(WebsiteAdminPlayer player)
        {
            await PlayerService.UnstuckPlayerAsync(player.Id);
        }
        public async void ResetSkills(WebsiteAdminPlayer player)
        {
            var result = await PlayerService.ResetPlayerSkillsAsync(player.Id);

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
        private string GetRestedTime(WebsiteAdminPlayer player)
        {
            return FormatTime(System.TimeSpan.FromSeconds(player.State.RestedTime));
        }
        private string FormatTime(TimeSpan time)
        {
            if (time.TotalSeconds < 60) return time.TotalSeconds + " seconds";
            if (time.TotalMinutes < 60)
                return (int)Math.Floor(time.TotalMinutes) + " minutes";

            return $"{time.Hours} hours, {time.Minutes} minutes";
        }

        private string GetTrainingSkillName(WebsiteAdminPlayer player)
        {
            if (string.IsNullOrEmpty(TrainingSkill))
                return null;

            if (TrainingSkill.Equals("all", StringComparison.OrdinalIgnoreCase))
                return "All";

            if (TrainingSkill.Equals("heal", StringComparison.OrdinalIgnoreCase))
                return "Healing";

            if (player == null || player.Skills == null)
                return null;

            var training = player.Skills.AsList().FirstOrDefault(IsTrainingSkill);
            return training?.Name;
        }
        private bool IsTrainingSkill(PlayerSkill skill)
        {
            if (string.IsNullOrEmpty(TrainingSkill))
                return false;

            if (skill.Name.StartsWith(TrainingSkill, StringComparison.OrdinalIgnoreCase))
                return true;

            if (TrainingSkill == "heal")
                return skill.Name.Equals("healing", StringComparison.OrdinalIgnoreCase);

            if (TrainingSkill.ToLower() == "all")
                return skill.Name.Equals("attack", StringComparison.OrdinalIgnoreCase) ||
                                skill.Name.Equals("defense", StringComparison.OrdinalIgnoreCase) ||
                                skill.Name.Equals("strength", StringComparison.OrdinalIgnoreCase);

            if (skill.Name.ToLower() == "attack" && TrainingSkill.ToLower() == "atk")
                return true;

            if (TrainingSkill.ToLower() == "mine" && skill.Name.Equals("mining", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }


    }

}
