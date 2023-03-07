using RavenNest.Tools.Actions;
using Shinobytes.Console.Forms;
using Shinobytes.Console.Forms.Graphics;
using System;

namespace RavenNest.Tools
{
    public partial class MainWindow : Window
    {
        private readonly AdjustPlayerExperienceAction expAdjuster;
        private readonly BuildUpdatePackageAction buildUpdatePackage;
        private readonly PlayerSkillRollback skillRollbackv0788;

        public MainWindow()
        {
            InitializeComponents();
            expAdjuster = new AdjustPlayerExperienceAction(ToolProgress, ToolStatus);
            buildUpdatePackage = new BuildUpdatePackageAction(ToolProgress, ToolStatus);
            skillRollbackv0788 = new PlayerSkillRollback(ToolProgress, ToolStatus, @"C:\Ravenfall\pre0.7.8.9-restorepoint\", @"C:\Ravenfall\pre0.7.8.9\");
        }

        private void BuildUpdatePackage_Invoke(object sender, EventArgs e)
        {
            buildUpdatePackage.Apply();
        }

        private void ExpBump_Invoke(object sender, EventArgs e)
        {
            expAdjuster.Apply();
        }

        private void SkillRollbackv0788_Invoke(object sender, EventArgs e)
        {
            skillRollbackv0788.Apply();
        }

        public override void Update(AppTime appTime)
        {
            base.Update(appTime);

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
                expAdjuster.Apply();
            }
            if (key.Key == ConsoleKey.F2)
            {
                buildUpdatePackage.Apply();
            }
            if (key.Key == ConsoleKey.F4)
            {
                skillRollbackv0788.Apply();
            }
            return base.OnKeyDown(key);
        }

    }
}
