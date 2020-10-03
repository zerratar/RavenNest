using System;
using System.Drawing;
using Shinobytes.Console.Forms;
using Shinobytes.Console.Forms.Graphics;

namespace RavenNest.Tools.Windows
{
    public class AboutWindow : Window
    {
        public AboutWindow()
        {
            BackgroundColor = ConsoleColor.Gray;
            Text = " About ";
            Size = new Size(40, 19);
            Position = new Point(40, 5);

            var title = "Shinobytes.Console.Forms";
            Controls.Add(new TextBlock(title, ConsoleColor.DarkMagenta, new Point(6, 1)));

            Controls.Add(new TextBlock(
                new String(AsciiCodes.BorderDouble_Horizontal, title.Length), ConsoleColor.DarkGray, new Point(6, 2)));

            Controls.Add(new TextBlock("Version 0.1.1", ConsoleColor.Black, new Point(12, 3)));
            Controls.Add(new TextBlock("by", ConsoleColor.Black, new Point(16, 5)));
            Controls.Add(new TextBlock("zerratar@gmail.com", ConsoleColor.Black, new Point(8, 7)));

            Controls.Add(new TextBlock(
                AsciiCodes.BorderSingle_SplitToRight +
                new String(AsciiCodes.BorderSingle_Horizontal, Size.Width - 4) +
                AsciiCodes.BorderSingle_SplitToLeft, ConsoleColor.DarkGray, new Point(-1, 9)));

            Controls.Add(new TextBlock("This software is licensed", ConsoleColor.Black, new Point(4, 11)));
            Controls.Add(new TextBlock("under MIT license so you", ConsoleColor.Black, new Point(5, 12)));
            Controls.Add(new TextBlock("can do whatever you want with it.", ConsoleColor.Black, new Point(2, 13)));

            var btn = new Button()
            {
                Text = "OK",
                BackgroundColor = ConsoleColor.Green,
                ForegroundColor = ConsoleColor.White,
                Position = new Point(13, 15),
                Size = new Size(8, 1)
            };
            btn.Invoke += (sender, args) =>
            {
                this.Hide();
            };
            Controls.Add(btn);
        }
        
        public override bool OnKeyDown(KeyInfo key)
        {
            if (key.Key == ConsoleKey.F1 || key.Key == ConsoleKey.Escape)
            {
                this.Hide();
            }
            return base.OnKeyDown(key);
        }
    }
}
