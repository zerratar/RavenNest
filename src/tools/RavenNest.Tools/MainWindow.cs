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

        private readonly PatreonSynchronizationAction patreonSync;

        public MainWindow()
        {
            InitializeComponents();
            expAdjuster = new AdjustPlayerExperienceAction(ToolProgress, ToolStatus);
            buildUpdatePackage = new BuildUpdatePackageAction(ToolProgress, ToolStatus);
            patreonSync = new PatreonSynchronizationAction(ToolProgress, ToolStatus);
        }

        private void BuildUpdatePackage_Invoke(object sender, EventArgs e)
        {
            buildUpdatePackage.Apply();
        }

        private void ExpBump_Invoke(object sender, EventArgs e)
        {
            expAdjuster.Apply();
        }

        private void PatreonSync_Invoke(object sender, EventArgs e)
        {
            patreonSync.Apply();
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
            if (key.Key == ConsoleKey.F3)
            {
                patreonSync.Apply();
            }
            return base.OnKeyDown(key);
        }

    }
}
