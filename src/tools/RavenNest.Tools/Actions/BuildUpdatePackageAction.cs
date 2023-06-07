using SevenZip;
using Shinobytes.Console.Forms;
using System;
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
        private const string UnityBuildFolderWin = @"C:\git\Ravenfall Legacy\Build";
        private const string UnityBuildFolderLinux = @"C:\git\Ravenfall Legacy\Build Linux";

        private const int MAX_REVISION = 10;
        private const int MAX_BUILD = 10;
        private const int MAX_MINOR = 10;

        /// <summary>
        /// Until we have an updated, the files are the same therefor we don't need to compress it twice.
        /// </summary>
        private const bool LinuxUpdateIsCopyOfReleaseBuild = true;


        private BuildState buildState = BuildState.Full_Windows;
        private readonly SevenZipCompressor compressor;
        private string targetBuildName;


        public int MajorIncrement = 0;
        public int MinorIncrement = 0;
        public int BuildIncrement = 0;
        public int RevisionIncrement = 1;
        private Version BuildVersion;

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
                PreserveDirectoryRoot = true,
                ArchiveFormat = OutArchiveFormat.SevenZip,
                CompressionMethod = CompressionMethod.Lzma2,
                CompressionLevel = CompressionLevel.Ultra,
                CompressionMode = CompressionMode.Create
            };
            compressor.Compressing += Compressor_Compressing;
            compressor.CompressionFinished += Compressor_CompressionFinished;
        }

        public ProgressBar ToolProgress { get; }
        public TextBlock ToolStatus { get; }
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

            BuildVersion = GetNextVersion();

            ToolStatus.Text = "Building Release Package...";
            ToolProgress.Indeterminate = false;
            buildState = BuildState.Full_Windows;
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
                    Arguments = $"publish -r win10-x64 -c release -p:PublishSingleFile=true -p:PublishTrimmed=true --self-contained true -o \"{UnityBuildFolderWin}\"",
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
                buildState = BuildState.Full_Windows;
            });
        }

        private async Task BuildPackageAsync()
        {
            await Task.Run(() =>
            {
                var allFiles = GetFiles();
                var buildFolder = GetTargetBuildFolder();
                targetBuildName = GetTargetName();
                ToolStatus.Text = "Building " + targetBuildName + "..";
                var targetFile = System.IO.Path.Combine(buildFolder, targetBuildName);
                if (System.IO.File.Exists(targetFile))
                    System.IO.File.Delete(targetFile);

                if (LinuxUpdateIsCopyOfReleaseBuild && buildState == BuildState.Update_Linux)
                {
                    System.IO.File.Copy(
                        System.IO.Path.Combine(buildFolder, GetReleaseFileName(false))
                        , targetFile, true);
                    Compressor_CompressionFinished(this, EventArgs.Empty);
                }
                else
                {
                    compressor.CompressFiles(targetFile, allFiles);
                }

            });
        }

        private string GetTargetName()
        {
            if (buildState == BuildState.Update_Windows)
            {
                return "update.7z";
            }

            if (buildState == BuildState.Update_Linux)
            {
                return "update-linux.7z";
            }

            var isWindowsBuild = IsWindowsBuild();
            var platformExtension = isWindowsBuild ? "" : "-linux";

            // var buildFolder = GetTargetBuildFolder();
            // while we could check both folders, its better to make sure both are up to date to the same version number. Therefor always use windows folder for this
            var existingArchives = System.IO.Directory.GetFiles(UnityBuildFolderWin, "Ravenfall.v*a-alpha.7z");
            if (existingArchives.Length == 0)
                return "ravenfall" + platformExtension + ".7z";

            //System.Version v = IncrementVersion(a.Version, MajorIncrement, MinorIncrement, BuildIncrement, RevisionIncrement);
            return GetReleaseFileName(isWindowsBuild);
        }

        private string GetReleaseFileName(bool windowsBuild)
        {
            var platformExtension = windowsBuild ? "" : "-linux";
            return "Ravenfall.v" + BuildVersion.ToString() + "a-alpha" + platformExtension + ".7z";
        }


        internal Version GetNextVersion()
        {
            var existingArchives = System.IO.Directory.GetFiles(UnityBuildFolderWin, "Ravenfall.v*a-alpha.7z");
            if (existingArchives.Length == 0) return null;
            var archives = existingArchives.Select(x => new { File = x, Version = GetVersion(x) }).OrderByDescending(x => x.Version).ToList();
            var a = archives.FirstOrDefault();
            if (a == null)
                return null;

            return IncrementVersion(a.Version, MajorIncrement, MinorIncrement, BuildIncrement, RevisionIncrement);
        }

        private System.Version IncrementVersion(System.Version version, int major, int minor, int build, int revision)
        {
            revision = version.Revision < 0 ? revision : version.Revision + revision;
            if (revision >= MAX_REVISION)
            {
                revision = 0;
                build++;
            }

            if (build >= MAX_BUILD)
            {
                build = 0;
                minor++;
            }

            if (minor >= MAX_MINOR)
            {
                minor = 0;
                major++;
            }

            build = version.Build + build;
            minor = version.Minor + minor;
            major = version.Major + major;
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
                case BuildState.Full_Windows:
                    ToolStatus.Text = "Building Update Package..";
                    buildState = BuildState.Update_Windows;
                    BuildPackageAsync();
                    break;
                case BuildState.Update_Windows:
                    ToolStatus.Text = "Building Linux Release Package..";
                    buildState = BuildState.Full_Linux;
                    BuildPackageAsync();
                    break;
                case BuildState.Full_Linux:
                    ToolStatus.Text = "Building Linux Update Package..";
                    buildState = BuildState.Update_Linux;
                    BuildPackageAsync();
                    break;
                case BuildState.Update_Linux:
                    ToolStatus.Text = "All packages built!";
                    buildState = BuildState.Completed;
                    break;
            }
        }

        private void Compressor_Compressing(object sender, ProgressEventArgs e)
        {
            ToolStatus.Text = "Building " + targetBuildName + "..";
            ToolProgress.MaxValue = 100;
            ToolProgress.Value = e.PercentDone;
        }

        private string GetTargetBuildFolder()
        {
            var winBuild = IsWindowsBuild();
            var buildFolder = winBuild ? UnityBuildFolderWin : UnityBuildFolderLinux;
            return buildFolder;
        }

        private bool IsWindowsBuild()
        {
            return buildState == BuildState.Full_Windows || buildState == BuildState.Update_Windows;
        }

        private string[] GetFiles()
        {
            var winBuild = IsWindowsBuild();
            var buildFolder = GetTargetBuildFolder();
            var legacyDataDir = System.IO.Path.Combine(buildFolder, "Ravenfall Legacy_Data");
            var targetDataDir = System.IO.Path.Combine(buildFolder, "Ravenfall_Data");

            if (System.IO.Directory.Exists(legacyDataDir))
            {
                if (System.IO.Directory.Exists(targetDataDir))
                {
                    System.IO.Directory.Delete(targetDataDir, true);
                }

                System.IO.Directory.Move(legacyDataDir, targetDataDir);
            }

            if (winBuild)
            {
                var legacyExe = System.IO.Path.Combine(buildFolder, "Ravenfall Legacy.exe");
                var targetExe = System.IO.Path.Combine(buildFolder, "Ravenfall.exe");

                if (System.IO.File.Exists(legacyExe))
                {
                    if (System.IO.File.Exists(targetExe))
                    {
                        System.IO.File.Delete(targetExe);
                    }

                    System.IO.File.Move(legacyExe, targetExe, true);
                }
            }

            var files = System.IO.Directory.GetFiles(buildFolder, "*", System.IO.SearchOption.AllDirectories);

            //var filesToRename = files.Where(x => x.Contains("Ravenfall Legacy")).ToList();
            return files.Where(FilterFiles).ToArray();
        }

        private bool FilterFiles(string x)
        {
            var lower = x.ToLower();
            var test = NotContains(lower, ".7z", "settings.json",
                "pub-sub.json", "autologin.conf", "tmpautologin.conf", "pubsub-tokens.json",
                "camera-positions.json", "game-settings.json", "state-data.json", "__DEBUG__",
                "\\update\\", "\\update\\",
                "\\data\\", "data\\stats", "data\\sounds",
                "_DoNotShip", "_ButDontShipItWithYourGame");
            if (buildState == BuildState.Update_Windows)
            {
                test &= NotContains(lower,
                    "fonts\\", "RavenWeave");
            }

            return test;
        }

        private static bool NotContains(string input, params string[] cases)
        {
            input = input.Replace("/", "\\");
            foreach (var c in cases)
            {
                var test = c.Replace("/", "\\");
                if (input.IndexOf(test, StringComparison.OrdinalIgnoreCase) >= 0)
                    return false;
            }

            return true;
        }

    }

    public enum BuildState
    {
        RavenBot,
        Full_Windows,
        Full_Linux,
        Update_Windows,
        Update_Linux,
        Completed
    }
}
