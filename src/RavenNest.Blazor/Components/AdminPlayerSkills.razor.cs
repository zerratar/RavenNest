using Microsoft.AspNetCore.Components;
using System;
using System.Linq;
using RavenNest.Blazor.Services;
using RavenNest.BusinessLogic.Extended;
using RavenNest.Models;

namespace RavenNest.Blazor.Components
{
    public partial class AdminPlayerSkills : ComponentBase
    {
        [Inject]
        PlayerService PlayerService { get; set; }
        [Inject]
        AuthService AuthService { get; set; }
        [Parameter]
        public WebsitePlayer Player { get; set; }

        [Parameter]
        public bool CanManage { get; set; }
        private TrainingSkill trainingSkill;
        private int currentHealth;

        private SessionInfo session;

        private bool isSailing;
        private bool modifySkillDialogVisible;
        private int modifyingSkillLevel = 0;
        private int modifyingSkillExperiencePercent = 0;

        private PlayerSkill modifyingSkill;

        private bool CanModify => session != null && session.Administrator;

        public string GetLastUpdateString(DateTime update)
        {
            var elapsed = DateTime.UtcNow - update;
            if (update <= DateTime.UnixEpoch)
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

        protected override void OnParametersSet()
        {
            if (Player == null)
                return;

            if (Player.State != null)
            {
                trainingSkill = PlayerService.GetTrainingSkill(Player.State); 
                isSailing = trainingSkill == null && string.IsNullOrEmpty(Player.State.Island);
                currentHealth = Player.State.Health;
            }
            else
            {
                currentHealth = Player.Skills.HealthLevel;
            }
        }

        protected override void OnInitialized()
        {
            session = AuthService.GetSession();
        }


        private void OnLevelModified(ChangeEventArgs evt)
        {
            if (evt.Value != null && int.TryParse(evt.Value?.ToString() ?? modifyingSkill.Level.ToString(), out var newLevel))
            {
                modifyingSkillLevel = newLevel;
            }
        }

        private void OnExperienceModified(ChangeEventArgs evt)
        {
            if (evt.Value != null && int.TryParse(evt.Value?.ToString() ?? "0", out var newExpPercent))
            {
                modifyingSkillExperiencePercent = newExpPercent;
            }
        }

        public async void ResetSkills()
        {
            var result = await PlayerService.ResetPlayerSkillsAsync(Player.Id);

            if (result)
            {
                this.Player = await PlayerService.GetPlayerAsync(Player.Id);
                await InvokeAsync(StateHasChanged);
            }
        }

        public async void CloneSkillsAndStateToMain()
        {
            var result = await PlayerService.CloneSkillsAndStateToMainAsync(Player.Id);

            if (result)
            {
                this.Player = await PlayerService.GetPlayerAsync(Player.Id);
                await InvokeAsync(StateHasChanged);
            }
        }

        public async void Unstuck()
        {
            await PlayerService.UnstuckPlayerAsync(Player.Id);
        }


        public async void ApplyModifySkill()
        {
            var result = await PlayerService.UpdatePlayerSkillAsync(Player.Id, modifyingSkill.Name, modifyingSkillLevel, modifyingSkillExperiencePercent / 100f);

            HideModifySkill();

            if (result)
            {
                this.Player = await PlayerService.GetPlayerAsync(Player.Id);
                await InvokeAsync(StateHasChanged);
            }
        }

        public void HideModifySkill()
        {
            modifySkillDialogVisible = false;
            modifyingSkill = null;
        }

        public void ShowModifySkill(PlayerSkill skill)
        {
            modifySkillDialogVisible = true;
            modifyingSkill = skill;
            modifyingSkillLevel = skill.Level;
            modifyingSkillExperiencePercent = (int)(skill.Percent * 100);
        }

        private string GetRestedTime()
        {
            return FormatTime(System.TimeSpan.FromSeconds(Player.State.RestedTime));
        }

        private string FormatTime(TimeSpan time)
        {
            if (time.TotalSeconds < 60) return time.TotalSeconds + " seconds";
            if (time.TotalMinutes < 60)
                return (int)Math.Floor(time.TotalMinutes) + " minutes";

            return $"{time.Hours} hours, {time.Minutes} minutes";
        }
        private string GetTrainingSkillName()
        {
            if (trainingSkill == null)
                return null;

            if (trainingSkill.Name.Equals("all", StringComparison.OrdinalIgnoreCase))
                return "All";

            if (trainingSkill.Name.Equals("heal", StringComparison.OrdinalIgnoreCase))
                return "Healing";

            if (Player == null || Player.Skills == null)
                return null;

            var training = Player.Skills.AsList().FirstOrDefault(IsTrainingSkill);
            return training?.Name;
        }

        private bool IsTrainingSkill(PlayerSkill skill)
        {
            var n = skill.Name.ToLower();

            if (isSailing && skill.Name.Equals("Sailing"))
                return true;

            if (trainingSkill == null)
                return false;

            var t = trainingSkill.Name.ToLower();

            if (n.StartsWith(t, StringComparison.OrdinalIgnoreCase))
                return true;

            if (t == "heal")
                return n.Equals("healing", StringComparison.OrdinalIgnoreCase);

            if (t == "all")
                return n.Equals("attack", StringComparison.OrdinalIgnoreCase) || n.Equals("defense", StringComparison.OrdinalIgnoreCase) || n.Equals("strength", StringComparison.OrdinalIgnoreCase);

            if (n.ToLower() == "attack" && t == "atk") return true;

            if (t == "mine" && n.Equals("mining", StringComparison.OrdinalIgnoreCase))
                return true;

            if (t == "gather" && n.Equals("gathering", StringComparison.OrdinalIgnoreCase))
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
    }
}
