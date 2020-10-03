using System;
using System.Drawing;
using Shinobytes.Console.Forms;
using Shinobytes.Console.Forms.Graphics;

namespace RavenNest.Tools.Windows
{
    public class SaveConfirmationWindow : Window
    {
        private Button yesBtn;
        private Button noBtn;

        public SaveConfirmationWindow()
        {
            BackgroundColor = ConsoleColor.Gray;
            Text = " Do you want to apply the changes? ";
            Size = new Size(64, 9);

            var posX = (System.Console.WindowWidth / 2) - Size.Width / 2;

            Position = new Point(posX, 5);

            var yesX = (int)(((Size.Width / 2f) - 3.5) - 7.5f) - 2;
            var noX = (int)(((Size.Width / 2f) - 3) + 7.5f) - 2;

            this.MessageLabel = new TextBlock("Do you want to apply and save the changes?", ConsoleColor.Red, new Point(0, 2));

            Controls.Add(MessageLabel);

            yesBtn = new Button
            {
                Text = "Yes",
                Position = new Point(yesX, 5),
                Size = new Size(7, 1),
                ForegroundColor = ConsoleColor.White,
                BackgroundColor = ConsoleColor.DarkCyan,
                DropShadow = true
            };
            yesBtn.Invoke += (sender, args) =>
            {
                this.DialogResult = true;
            };
            Controls.Add(yesBtn);

            noBtn = new Button
            {
                Text = "No",
                Position = new Point(noX, 5),
                Size = new Size(6, 1),
                BackgroundColor = ConsoleColor.White,
                DropShadow = true
            };
            noBtn.Invoke += (sender, args) =>
            {
                this.DialogResult = false;
            };
            Controls.Add(noBtn);
        }

        public TextBlock MessageLabel { get; }

        public override void Update(AppTime appTime)
        {
            base.Update(appTime);
            var len = MessageLabel.Text.Length;
            var newWidth = len + 7;
            if (this.Size.Width < newWidth)
                this.Size = new Size(newWidth, this.Size.Height);

            MessageLabel.Position = new Point(this.Size.Width / 2 - len / 2 - 2, MessageLabel.Position.Y);
        }

        public void MoveToCenter()
        {
            var posX = (System.Console.WindowWidth / 2) - Size.Width / 2;
            Position = new Point(posX, Position.Y);
            var yesX = (int)(((Size.Width / 2f) - 3.5) - 7.5f) - 2;
            var noX = (int)(((Size.Width / 2f) - 3) + 7.5f) - 2;

            yesBtn.Position = new Point(yesX, yesBtn.Position.Y);
            noBtn.Position = new Point(noX, noBtn.Position.Y);
        }

        public override bool OnKeyDown(KeyInfo key)
        {
            if (key.Key == ConsoleKey.Escape)
            {
                this.Hide();
            }
            return base.OnKeyDown(key);
        }
    }
}
