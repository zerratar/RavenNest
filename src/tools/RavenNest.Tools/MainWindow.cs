using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using RavenNest.BusinessLogic;
using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;
using RavenNest.Tools.Windows;
using Shinobytes.Console.Forms;
using Shinobytes.Console.Forms.Graphics;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RavenNest.Tools
{
    public partial class MainWindow : Window
    {
        private SaveConfirmationWindow confirmDialog;

        public MainWindow()
        {
            InitializeComponents();
            ExpBump.Invoke += ExpBump_Invoke;
            confirmDialog = new SaveConfirmationWindow();
        }

        private void ExpBump_Invoke(object sender, EventArgs e)
        {
            ReducePlayerExperience();
        }

        public override void Update(AppTime appTime)
        {
            base.Update(appTime);

            confirmDialog.MoveToCenter();

            var w = Console.WindowWidth;
            var h = Console.WindowHeight;
            var wHalf = w / 2;
            var hHalf = h / 2;

            //Logo.Position = new System.Drawing.Point(wHalf - Logo.ImageSource.Width / 2, hHalf - Logo.ImageSource.Height / 2);

            LogoLabel.Position = new System.Drawing.Point(wHalf - LogoLabelWidth / 2, hHalf - 9);

            ToolStatus.Position = new System.Drawing.Point(wHalf - ToolStatus.Text.Length / 2, hHalf - 2);
            ToolProgress.Position = new System.Drawing.Point(wHalf - ToolProgress.Size.Width / 2, hHalf);
        }

        public override bool OnKeyDown(KeyInfo key)
        {
            if (key.Key == ConsoleKey.F1)
            {
                ReducePlayerExperience();
            }
            return base.OnKeyDown(key);
        }

        public void ReducePlayerExperience()
        {
            ToolStatus.Text = "Loading backup files...";
            Task.Run(() =>
            {
                var backupProvider = new GameDataBackupProvider();
                var rpBefore = backupProvider.GetRestorePoint(@"C:\backup\637367260672779696", typeof(Character), typeof(Skills));
                var rpAfter = backupProvider.GetRestorePoint(@"C:\backup\637369626496299730", typeof(Character), typeof(Skills));

                var skillsIds = Ravenfall.RavenNest.GetAffectedSkillsIds();
                var oldSkillsBefore = rpBefore.Get<Skills>();
                var oldSkillsAfter = rpAfter.Get<Skills>();
                var oldChars = rpBefore.Get<Character>();

                var builder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

                var configuration = builder.Build();

                var appSettingsSection = configuration.GetSection("AppSettings");
                var appSettings = appSettingsSection.Get<AppSettings>();

                var dbContextProvider = new RavenfallDbContextProvider(Options.Create(appSettings));

                ToolStatus.Text = "Processing " + skillsIds.Length + " players...";
                var con = dbContextProvider.Get();
                {
                    var skillsDoubled = 0;
                    var zeroSkills = 0;
                    var skillDeltaCount = 0;
                    var updated = false;
                    var playersUpdated = 0;
                    var processIndex = 0;
                    foreach (var skillId in skillsIds)
                    {

                        ++processIndex;
                        ToolProgress.Value = (float)Math.Floor((processIndex / (float)skillsIds.Length) * 100d);

                        var skillBefore = oldSkillsBefore.FirstOrDefault(x => x.Id == skillId);
                        var skillAfter = oldSkillsAfter.FirstOrDefault(x => x.Id == skillId);
                        var skillListBefore = skillBefore.GetSkills();

                        var character = oldChars.FirstOrDefault(x => x.SkillsId == skillId);
                        var skillListAfter = skillAfter.GetSkills();

                        var curSkills = con.Skills.FirstOrDefault(x => x.Id == skillId);
                        var skillsUpdated = false;
                        for (int i = 0; i < skillListAfter.Count; i++)
                        {
                            var sb = skillListBefore[i];
                            var sa = skillListAfter[i];

                            var exp = sb.Experience;
                            var totalExp = exp;
                            var level = sb.Level;

                            if (sb.Level == 0)
                            {
                                ++zeroSkills;
                                level = GameMath.OLD_ExperienceToLevel(sb.Experience);
                                totalExp = sb.Experience;
                                exp = totalExp - GameMath.OLD_LevelToExperience(level);
                            }

                            if (level == 1 && exp == 0)
                            {
                                continue;
                            }

                            var levelDelta = sa.Level - level;
                            var expDelta = 0M;

                            if (sa.Level > 0 && levelDelta > 0)
                            {
                                expDelta = sa.Experience;
                                var saLevel = sa.Level;
                                for (var j = 1; j <= levelDelta; ++j)
                                {
                                    expDelta += GameMath.ExperienceForLevel(level + j);
                                }
                            }

                            if (levelDelta != 0 || expDelta > 0)
                            {
                                if (levelDelta != 0)
                                    ++skillDeltaCount;
                                if (expDelta >= totalExp * 0.95m)
                                {
                                    skillsUpdated = true;
                                    Ravenfall.RavenNest.ReduceSkillExp(i, curSkills, totalExp);
                                }
                            }
                        }

                        if (skillsUpdated)
                        {
                            ToolStatus.Text = playersUpdated + " players will be updated. " + processIndex + " players processed.";
                            ++playersUpdated;
                            updated = true;
                            con.Update(curSkills);
                        }
                    }

                    //ToolStatus.Text = playersUpdated + " players will be updated. Are you sure you want to continue?";
                    confirmDialog.MessageLabel.Text = playersUpdated + " players will be updated. Are you sure you want to continue?";
                    confirmDialog.Show(result =>
                    {
                        if (result.GetValueOrDefault())
                        {
                            ToolStatus.Text = "Saving players...";
                            if (updated)
                            {
                                con.SaveChanges();
                            }
                            ToolStatus.Text = playersUpdated + " players saved.";
                        }
                        else
                        {
                            ToolStatus.Text = "No players was saved. Save cancelled.";
                            ToolProgress.Value = 0;
                        }

                        con.Dispose();
                    });
                }
            });
        }
    }
}
