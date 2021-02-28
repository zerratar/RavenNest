using SevenZip;
using Shinobytes.Console.Forms;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace RavenNest.Tools.Actions
{
    public class BuildUpdatePackageAction
    {
        private const string RavenBotFolder = @"C:\git\RavenBot";
        private const string UnityBuildFolder = @"C:\git\Ravenfall-Legacy\Build";
        private BuildState buildState = BuildState.Full;

        public BuildUpdatePackageAction(
          ProgressBar toolProgress,
          TextBlock toolStatus)
        {
            this.ToolProgress = toolProgress;
            this.ToolStatus = toolStatus;

            var p = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var dll = System.IO.Path.Combine(p, "libs", "7z.dll");

            SevenZipCompressor.SetLibraryPath(dll);
            compressor = new SevenZipCompressor
            {
                ArchiveFormat = OutArchiveFormat.SevenZip,
                PreserveDirectoryRoot = true,
                CompressionLevel = CompressionLevel.Ultra,
                CompressionMethod = CompressionMethod.Lzma2,
                CompressionMode = CompressionMode.Create
            };
            compressor.Compressing += Compressor_Compressing;
            compressor.CompressionFinished += Compressor_CompressionFinished;
        }

        public ProgressBar ToolProgress { get; }
        public TextBlock ToolStatus { get; }

        private SevenZipCompressor compressor;
        private string targetBuildName;

        public async void Apply()
        {
            if (buildState == BuildState.RavenBot)
            {
                ToolStatus.Text = "Building RavenBot...";
                ToolProgress.Indeterminate = true;
                await BuildRavenBotAsync();
                ToolStatus.Text = "RavenBot built!";
                // wait for a second to ensure the ravenbot build was done.
                await Task.Delay(1000);
            }

            ToolStatus.Text = "Building Release Package...";
            ToolProgress.Indeterminate = false;
            buildState = BuildState.Full;
            await BuildPackageAsync();
        }

        private async Task BuildRavenBotAsync()
        {
            await Task.Run(() =>
            {
                // dotnet publish -r win10-x64 -c release -p:PublishSingleFile=true -p:PublishTrimmed=true --self-contained true -o "C:\git\Ravenfall-Legacy\Build\"
                var startInfo = new ProcessStartInfo
                {
                    FileName = "dotnet.exe",
                    WorkingDirectory = RavenBotFolder,
                    Arguments = $"publish -r win10-x64 -c release -p:PublishSingleFile=true -p:PublishTrimmed=true --self-contained true -o \"{UnityBuildFolder}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                };
                var readResult = "";
                using (var process = System.Diagnostics.Process.Start(startInfo))
                {
                    if (process != null)
                    {
                        readResult = process.StandardOutput.ReadToEnd();
                        process.WaitForExit();
                    }
                }
                buildState = BuildState.Full;
            });
        }

        private async Task BuildPackageAsync()
        {
            await Task.Run(() =>
            {
                var allFiles = GetFiles();
                targetBuildName = GetTargetName();
                ToolStatus.Text = "Building " + targetBuildName + "..";
                var targetFile = System.IO.Path.Combine(UnityBuildFolder, targetBuildName);
                if (System.IO.File.Exists(targetFile))
                    System.IO.File.Delete(targetFile);

                compressor.CompressFiles(targetFile, allFiles);
            });
        }

        private string GetTargetName()
        {
            if (buildState == BuildState.Update)
            {
                return "update.7z";
            }

            var existingArchives = System.IO.Directory.GetFiles(UnityBuildFolder, "Ravenfall.v*a-alpha.7z");
            if (existingArchives.Length == 0)
                return "ravenfall.7z";

            var archives = existingArchives.Select(x => new { File = x, Version = GetVersion(x) }).OrderByDescending(x => x.Version).ToList();
            var a = archives.FirstOrDefault();
            if (a == null)
                return "ravenfall.7z";

            System.Version v = IncrementVersion(a.Version, 0, 0, 0, 1);
            return "Ravenfall.v" + v.ToString() + "a-alpha.7z";
        }

        private System.Version IncrementVersion(System.Version version, int major, int minor, int build, int revision)
        {
            major = version.Major < 0 ? major : version.Major + major;
            minor = version.Minor < 0 ? minor : version.Minor + minor;
            build = version.Build < 0 ? build : version.Build + build;
            revision = version.Revision < 0 ? revision : version.Revision + revision;
            return new System.Version(major, minor, build, revision);
        }

        private System.Version GetVersion(string x)
        {
            var vString = System.IO.Path.GetFileNameWithoutExtension(x)
                .Replace("Ravenfall.v", "")
                .Replace("a-alpha", "");

            if (System.Version.TryParse(vString, out var version))
                return version;

            return new System.Version();
        }

        private void Compressor_CompressionFinished(object sender, System.EventArgs e)
        {
            switch (buildState)
            {
                case BuildState.Full:
                    ToolStatus.Text = "Building Update Package..";
                    buildState = BuildState.Update;
                    BuildPackageAsync();
                    break;
                case BuildState.Update:
                    ToolStatus.Text = "Building Update All Done.";
                    break;
            }
        }

        private void Compressor_Compressing(object sender, ProgressEventArgs e)
        {
            ToolStatus.Text = "Building " + targetBuildName + "..";
            ToolProgress.MaxValue = 100;
            ToolProgress.Value = e.PercentDone;
        }

        private string[] GetFiles()
        {
            var legacyDataDir = System.IO.Path.Combine(UnityBuildFolder, "Ravenfall Legacy_Data");
            var targetDataDir = System.IO.Path.Combine(UnityBuildFolder, "Ravenfall_Data");
            var legacyExe = System.IO.Path.Combine(UnityBuildFolder, "Ravenfall Legacy.exe");
            var targetExe = System.IO.Path.Combine(UnityBuildFolder, "Ravenfall.exe");

            if (System.IO.Directory.Exists(legacyDataDir))
            {
                if (System.IO.Directory.Exists(targetDataDir))
                {
                    System.IO.Directory.Delete(targetDataDir, true);
                }

                System.IO.Directory.Move(legacyDataDir, targetDataDir);
            }

            if (System.IO.File.Exists(legacyExe))
            {
                if (System.IO.File.Exists(targetExe))
                {
                    System.IO.File.Delete(targetExe);
                }

                System.IO.File.Move(legacyExe, targetExe, true);
            }


            var files = System.IO.Directory.GetFiles(UnityBuildFolder, "*", System.IO.SearchOption.AllDirectories);

            //var filesToRename = files.Where(x => x.Contains("Ravenfall Legacy")).ToList();
            return files.Where(FilterFiles).ToArray();
        }

        private bool FilterFiles(string x)
        {
            var lower = x.ToLower();
            var test = NotContains(lower, ".7z", "settings.json");
            if (buildState == BuildState.Update)
            {
                test &= NotContains(lower, "\\data\\", "/data/", "fonts\\", "fonts/", "RavenWeave");
            }

            return test;
        }

        private static bool NotContains(string input, params string[] cases)
        {
            foreach (var c in cases)
            {
                if (input.IndexOf(c, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return false;
            }

            return true;
        }
    }

    public enum BuildState
    {
        RavenBot,
        Full,
        Update
    }
}
