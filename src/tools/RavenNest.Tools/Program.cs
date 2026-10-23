using RavenNest.Tools.Actions;
using Shinobytes.Console.Forms;
using System;

namespace RavenNest.Tools
{
    class Program
    {
        static int Main(string[] args)
        {
            // "RavenNest.Tools package [--no-bot]" builds the release packages without the window
            if (args.Length > 0 && args[0].Equals("package", StringComparison.OrdinalIgnoreCase))
            {
                var packager = new ReleasePackager(x => System.Console.WriteLine(x), null);
                try
                {
                    packager.Run(includeBot: Array.IndexOf(args, "--no-bot") < 0);
                    System.Console.WriteLine(packager.LogText);
                    return 0;
                }
                catch (Exception exc)
                {
                    System.Console.WriteLine(packager.LogText);
                    System.Console.WriteLine("Packaging failed: " + exc.Message);
                    return 1;
                }
            }

            Application.Run(new MainWindow());
            return 0;
        }
    }
}
