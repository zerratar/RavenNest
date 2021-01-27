using Shinobytes.Console.Forms;
using System;
using System.Drawing;

namespace RavenNest.Tools
{
    public partial class MainWindow
    {
        private StatusStrip statusStrip;
        private ProgressBar ToolProgress;

        private TextBlock LogoLabel;
        private int LogoLabelWidth = 54;

        private TextBlock ToolStatus;
        private MenuItem ExpBump, BuildUpdatePackage;
        //private Shinobytes.Console.Forms.Image Logo;

        public void InitializeComponents()
        {
            Console.Title = "Ravenfall Tools";
            this.Text = "Ravenfall Tools";

            this.BackgroundColor = ConsoleColor.DarkBlue;

            //var image = Shinobytes.Console.Forms.Graphics.ConsoleImage.FromFile("");

            //this.Logo = new Shinobytes.Console.Forms.Image()
            //{
            //    Position = new Point(0, -4),
            //    ImageSource = ConsoleImage.FromFile("Logo.png")
            //};
            //this.Controls.Add(Logo);

            LogoLabel = new TextBlock
            {
                Position = new Point(0, 0),
                Text =
                @"   _ \                               _|         |  | " + "\n" +
                @"  |   |   _` | \ \   /  _ \  __ \   |     _` |  |  | " + "\n" +
                @"  __ <   (   |  \ \ /   __/  |   |  __|  (   |  |  | " + "\n" +
                @" _| \_\ \__,_|   \_/  \___| _|  _| _|   \__,_| _| _| "
            };
            this.Controls.Add(LogoLabel);

            ToolStatus = new TextBlock
            {
                Text = "Run a tool under the Tools menu",
                Position = new Point(4, 20)
            };
            this.Controls.Add(ToolStatus);

            ToolProgress = new ProgressBar
            {
                Position = new Point(4, 22),
                Size = new Size(50, 1),
                Value = 0
            };

            // pb.Indeterminate = true;
            // pb.ProgressValuePosition = ProgressValuePosition.Above;
            // pb.ProgressBackColor = this.BackgroundColor; // << for "transparent" background
            this.Controls.Add(ToolProgress);

            var menuStrip = new MenuStrip();

            var fileMenu = new MenuItem("&File");
            {
                var exitButton = new MenuItem("E&xit");
                exitButton.Invoke += (sender, args) =>
                {
                    Application.Exit();
                };
                fileMenu.SubItems.Add(exitButton);
                menuStrip.Controls.Add(fileMenu);
            }

            var editMenu = new MenuItem("&Tools");
            {
                editMenu.MinWidth = 15;

                BuildUpdatePackage = new MenuItem("Build Update Package");
                BuildUpdatePackage.Invoke += BuildUpdatePackage_Invoke;
                editMenu.SubItems.Add(BuildUpdatePackage);

                ExpBump = new MenuItem("Fix Exp Bump");
                ExpBump.Invoke += ExpBump_Invoke;
                ExpBump.IsEnabled = false;
                editMenu.SubItems.Add(ExpBump);
                menuStrip.Controls.Add(editMenu);
            }

            this.Controls.Add(menuStrip);

            statusStrip = new StatusStrip();
            statusStrip.Controls.Add(new TextBlock("F1=Fix Exp Bump")
            {
                ForegroundColor = ConsoleColor.Black
            });
            statusStrip.Controls.Add(new TextBlock("F2=Build Update Package")
            {
                ForegroundColor = ConsoleColor.Black
            });
            this.Controls.Add(statusStrip);
        }
    }
}
