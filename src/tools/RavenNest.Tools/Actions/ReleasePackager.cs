using SevenZip;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace RavenNest.Tools.Actions
{
    /// <summary>
    /// Builds every file a GitHub release of the game carries:
    ///   Ravenfall.v{version}-alpha.7z        the full Windows game, with the RavenWeave updater
    ///   update.7z                            the Windows update, with the updater in updater\
    ///   Ravenfall.v{version}-alpha-linux.7z  the full Linux game, with update.sh and install.sh
    ///   update-linux.7z                      the Linux update, with update.sh in its updater folder
    ///   RavenBot.7z / RavenBot-linux.7z      the local bot, self contained, for streamers that run it themselves
    ///   SHA256SUMS.txt and release-notes.md
    /// The server picks the assets by name (RavenNest.BusinessLogic/Github.cs): "update." and "update-linux" are the
    /// updates the game downloads, "Ravenfall.v" the full downloads. Keep those names.
    ///
    /// The game builds are the Unity release builds (CoreCLR, not Development) made into Build-release-windows and
    /// Build-release-linux. They are named "Ravenfall Legacy" by Unity; in the packages the files are called
    /// Ravenfall.exe / Ravenfall.x86_64 / Ravenfall_Data like every earlier release, so updates land on the same files.
    ///
    /// The updater (RavenWeave on Windows, update.sh on Linux) is in use while an update installs, so an update never
    /// replaces it directly: it carries the new one in updater\, which the old updater installs like any folder, and
    /// the game moves it into place when it next starts (UpdaterSwap in the game). RavenWeave is built from its repo.
    /// </summary>
    public class ReleasePackager
    {
        public const string GameFolder = @"G:\Ravenfall\Projects\Ravenfall Legacy";
        public const string RavenBotFolder = @"G:\Ravenfall\Projects\RavenBot";

        public static readonly string WindowsBuild = Path.Combine(GameFolder, "Build-release-windows");
        public static readonly string LinuxBuild = Path.Combine(GameFolder, "Build-release-linux");
        // the RavenWeave updater, built from its repo (.NET Framework 4.8, so it needs nothing installed)
        public const string RavenWeaveProject = @"G:\Ravenfall\Projects\RavenWeave\src\RavenWeave.WPF\RavenWeave.WPF.csproj";
        // our own scripts, versioned in the game repo
        public static readonly string WindowsScripts = Path.Combine(GameFolder, "tools", "release", "windows");
        public static readonly string LinuxScripts = Path.Combine(GameFolder, "tools", "release", "linux");
        public static readonly string ReleaseNotesFolder = Path.Combine(GameFolder, "docs", "releases");
        public static readonly string OutputRoot = Path.Combine(GameFolder, "Build-release-packages");

        private const string UnityName = "Ravenfall Legacy";
        private const string ShippedName = "Ravenfall";

        // files a build folder can pick up from running the game in place, or that Unity says not to ship
        private static readonly string[] Excluded =
        {
            "_BackUpThisFolder_ButDontShipItWithYourGame", "_BurstDebugInformation_DoNotShip", "_DoNotShip",
            "settings.json", "pub-sub.json", "autologin.conf", "pubsub-tokens.json", "camera-positions.json",
            "state-data.json", "tavern-highscore", "__DEBUG__",
        };

        private static readonly string[] ExcludedFolders = { "update", "data", "logs", "states" };

        private readonly Action<string> status;
        private readonly Action<int> progress;
        private readonly StringBuilder log = new StringBuilder();

        public ReleasePackager(Action<string> status, Action<int> progress)
        {
            this.status = status ?? (_ => { });
            this.progress = progress ?? (_ => { });
        }

        public string OutputFolder { get; private set; }
        public string Version { get; private set; }

        /// <summary>Checks the builds and returns the game version they carry, e.g. "0.9.5.0a".</summary>
        public string CheckBuilds()
        {
            var windowsVersion = CheckBuild(WindowsBuild, UnityName + ".exe");
            var linuxVersion = CheckBuild(LinuxBuild, UnityName + ".x86_64");
            if (windowsVersion != linuxVersion)
                throw new InvalidOperationException($"The Windows build is {windowsVersion} but the Linux build is {linuxVersion}. Build both again.");

            if (!File.Exists(RavenWeaveProject))
                throw new InvalidOperationException("The RavenWeave updater project is missing: " + RavenWeaveProject);

            foreach (var script in new[] { Path.Combine(WindowsScripts, "batch-mode.bat"), Path.Combine(LinuxScripts, "update.sh"), Path.Combine(LinuxScripts, "install.sh") })
            {
                if (!File.Exists(script)) throw new InvalidOperationException("Missing " + script);
            }

            foreach (var script in Directory.GetFiles(LinuxScripts, "*.sh"))
            {
                if (File.ReadAllText(script).Contains('\r'))
                    throw new InvalidOperationException(script + " has Windows line endings, bash will not run it.");
            }

            return Version = windowsVersion;
        }

        private static string CheckBuild(string folder, string executable)
        {
            if (!File.Exists(Path.Combine(folder, executable)))
                throw new InvalidOperationException("No build in " + folder + " (" + executable + " is missing).");

            var data = Path.Combine(folder, UnityName + "_Data");
            var boot = File.ReadAllText(Path.Combine(data, "boot.config"));
            if (boot.Contains("player-connection-mode") || boot.Contains("player-connection-debug"))
                throw new InvalidOperationException(folder + " is a Development build. Release builds must not be Development builds.");

            if (!Directory.Exists(Path.Combine(folder, "CoreCLR")))
                throw new InvalidOperationException(folder + " is not a CoreCLR build. The release uses the CoreCLR scripting backend.");

            // Application.version (PlayerSettings.bundleVersion) is stored as plain text in globalgamemanagers
            var bytes = File.ReadAllBytes(Path.Combine(data, "globalgamemanagers"));
            var text = Encoding.Latin1.GetString(bytes);
            var match = Regex.Match(text, @"(?<![\d.])\d+\.\d+\.\d+\.\d+a(?![\w.])");
            if (!match.Success)
                throw new InvalidOperationException("Could not find the game version in " + data);

            return match.Value;
        }

        /// <summary>
        /// Makes the release builds first, with a batch mode Unity running ReleaseBuild.BuildFromCommandLine (game repo,
        /// Assets/Scripts/Editor/ReleaseBuild.cs). Unity cannot open a project that is already open, so with the editor
        /// running use Ravenfall > Release > Build and Package there instead.
        /// </summary>
        public void BuildGame()
        {
            if (ProjectIsOpen())
                throw new InvalidOperationException("The game project is open in Unity. Close it, or use Ravenfall > Release > Build and Package in the editor.");

            var unity = UnityEditorPath();
            var unityLog = Path.Combine(GameFolder, "Logs", "release-build-unity.log");
            var buildLog = Path.Combine(GameFolder, "Logs", "release-build.txt");
            status("Building the game for Windows and Linux in Unity, this takes a while (log: " + unityLog + ")");
            var watch = Stopwatch.StartNew();

            var startInfo = new ProcessStartInfo(unity,
                $"-batchmode -projectPath \"{GameFolder}\" -executeMethod ReleaseBuild.BuildFromCommandLine -logFile \"{unityLog}\"")
            {
                UseShellExecute = false,
            };

            using (var process = Process.Start(startInfo))
            {
                process.WaitForExit();
                if (File.Exists(buildLog))
                    Log(File.ReadAllText(buildLog).TrimEnd());

                if (process.ExitCode != 0)
                    throw new InvalidOperationException($"The Unity build failed (exit code {process.ExitCode}), see {buildLog} and {unityLog}");
            }

            Log($"Unity builds done in {watch.Elapsed:hh\\:mm\\:ss}");
        }

        private static bool ProjectIsOpen()
        {
            // an open editor holds Temp/UnityLockfile open
            var lockFile = Path.Combine(GameFolder, "Temp", "UnityLockfile");
            if (!File.Exists(lockFile))
                return false;
            try
            {
                using (File.Open(lockFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                return false;
            }
            catch (IOException)
            {
                return true;
            }
        }

        private static string UnityEditorPath()
        {
            // the editor version the project is on, where Unity Hub installs it
            var version = File.ReadAllLines(Path.Combine(GameFolder, "ProjectSettings", "ProjectVersion.txt"))
                .First(x => x.StartsWith("m_EditorVersion:")).Split(':')[1].Trim();
            var path = Path.Combine(@"C:\Program Files\Unity\Hub\Editor", version, "Editor", "Unity.exe");
            if (!File.Exists(path))
                throw new InvalidOperationException("Unity " + version + " is not installed at " + path);
            return path;
        }

        public void Run(bool includeBot = true, bool buildGame = false)
        {
            if (buildGame)
                BuildGame();

            var version = CheckBuilds();
            var tag = "v" + version + "-alpha";
            OutputFolder = Path.Combine(OutputRoot, tag);
            Directory.CreateDirectory(OutputFolder);
            foreach (var old in Directory.GetFiles(OutputFolder))
                File.Delete(old);

            status("Packaging Ravenfall " + tag);

            var updater = BuildRavenWeave();

            var windows = GameFiles(WindowsBuild);
            Add(windows, WindowsScripts);
            var windowsFull = new Dictionary<string, string>(windows);
            Add(windowsFull, updater);
            var windowsUpdate = new Dictionary<string, string>(windows);
            Add(windowsUpdate, updater, "updater");

            var linux = GameFiles(LinuxBuild);
            var linuxFull = new Dictionary<string, string>(linux);
            Add(linuxFull, LinuxScripts);
            // update.sh goes in updater\ in the update: the old script is running while the update installs
            var linuxUpdate = new Dictionary<string, string>(linux);
            foreach (var script in Directory.GetFiles(LinuxScripts))
            {
                var name = Path.GetFileName(script);
                linuxUpdate[name.Equals("update.sh", StringComparison.OrdinalIgnoreCase) ? Path.Combine("updater", name) : name] = script;
            }

            var fullWindows = "Ravenfall." + tag + ".7z";
            var fullLinux = "Ravenfall." + tag + "-linux.7z";

            Compress(fullWindows, windowsFull);
            Compress("update.7z", windowsUpdate);
            Compress(fullLinux, linuxFull);
            Compress("update-linux.7z", linuxUpdate);

            if (includeBot)
            {
                PackageBot("RavenBot.7z", Path.Combine(RavenBotFolder, "src", "RavenBot", "LocalBot.csproj"), "net8.0-windows7.0", "win-x64");
                PackageBot("RavenBot-linux.7z", Path.Combine(RavenBotFolder, "src", "LocalBot.Linux", "LocalBot.Linux.csproj"), "net8.0", "linux-x64");
            }

            var notes = Path.Combine(ReleaseNotesFolder, tag + ".md");
            if (File.Exists(notes))
                File.Copy(notes, Path.Combine(OutputFolder, "release-notes.md"), true);
            else
                Log("No release notes at " + notes);

            WriteChecksums();
            File.WriteAllText(Path.Combine(OutputFolder, "package-log.txt"), log.ToString());
            status("All packages built in " + OutputFolder);
        }

        /// <summary>The build's files by their name in the package, with Unity's "Ravenfall Legacy" names shipped as "Ravenfall".</summary>
        private Dictionary<string, string> GameFiles(string folder)
        {
            var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
            {
                var name = Path.GetRelativePath(folder, file);
                if (!Ship(name))
                    continue;

                if (name.StartsWith(UnityName + "_Data" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    name = ShippedName + "_Data" + name.Substring(UnityName.Length + "_Data".Length);
                else if (name.Equals(UnityName + ".exe", StringComparison.OrdinalIgnoreCase))
                    name = ShippedName + ".exe";
                else if (name.Equals(UnityName + ".x86_64", StringComparison.OrdinalIgnoreCase))
                    name = ShippedName + ".x86_64";

                files[name] = file;
            }
            return files;
        }

        private static bool Ship(string relativePath)
        {
            if (Excluded.Any(x => relativePath.Contains(x, StringComparison.OrdinalIgnoreCase)))
                return false;

            var first = relativePath.Split(Path.DirectorySeparatorChar)[0];
            if (ExcludedFolders.Any(x => first.Equals(x, StringComparison.OrdinalIgnoreCase)))
                return false;

            var extension = Path.GetExtension(relativePath).ToLowerInvariant();
            if (extension == ".7z" || extension == ".pdb" || extension == ".log" || relativePath.EndsWith("_s.debug", StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        private static void Add(Dictionary<string, string> files, string folder, string under = null)
        {
            foreach (var file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
            {
                var name = Path.GetRelativePath(folder, file);
                files[under != null ? Path.Combine(under, name) : name] = file;
            }
        }

        /// <summary>Builds RavenWeave and returns a folder with its files (no debug symbols).</summary>
        private string BuildRavenWeave()
        {
            status("Building the RavenWeave updater");
            var output = Path.Combine(Path.GetTempPath(), "ravenfall-release-ravenweave");
            if (Directory.Exists(output)) Directory.Delete(output, true);

            var startInfo = new ProcessStartInfo("dotnet", $"build \"{RavenWeaveProject}\" -c Release -o \"{output}\" -nologo -v q")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using (var process = Process.Start(startInfo))
            {
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();
                process.WaitForExit();
                if (process.ExitCode != 0 || !File.Exists(Path.Combine(output, "RavenWeave.exe")))
                {
                    Log(stdout.Result + stderr.Result);
                    throw new InvalidOperationException("Building RavenWeave failed, see package-log.txt");
                }
            }

            foreach (var pdb in Directory.GetFiles(output, "*.pdb")) File.Delete(pdb);
            Log("RavenWeave: " + string.Join(", ", Directory.GetFiles(output).Select(Path.GetFileName)));
            return output;
        }

        private void Compress(string archive, Dictionary<string, string> files)
        {
            var target = Path.Combine(OutputFolder, archive);
            var bytes = files.Values.Sum(x => new FileInfo(x).Length);
            status($"Building {archive} ({files.Count} files, {bytes / 1024 / 1024} MB)");
            var watch = Stopwatch.StartNew();

            var compressor = CreateCompressor();
            compressor.Compressing += (s, e) => progress(e.PercentDone);
            compressor.CompressFileDictionary(files, target);

            var size = new FileInfo(target).Length;
            Log($"{archive}: {files.Count} files, {bytes / 1024 / 1024} MB packed to {size / 1024 / 1024} MB in {watch.Elapsed:mm\\:ss}");
            progress(100);
        }

        private static SevenZipCompressor CreateCompressor()
        {
            var p = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            SevenZipBase.SetLibraryPath(Path.Combine(p, "libs", "7z.dll"));
            var compressor = new SevenZipCompressor
            {
                ArchiveFormat = OutArchiveFormat.SevenZip,
                CompressionMethod = CompressionMethod.Lzma2,
                CompressionLevel = CompressionLevel.Ultra,
                CompressionMode = CompressionMode.Create,
            };
            compressor.CustomParameters.Add("mt", "on");
            return compressor;
        }

        private void PackageBot(string archive, string project, string framework, string runtime)
        {
            status("Publishing the local bot for " + runtime);
            var output = Path.Combine(Path.GetTempPath(), "ravenfall-release-bot-" + runtime);
            if (Directory.Exists(output))
                Directory.Delete(output, true);

            // self contained, so streamers do not need to install .NET; one file, compressed
            var arguments = $"publish \"{project}\" -c Release -f {framework} -r {runtime} --self-contained true -p:PublishSingleFile=true "
                + "-p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none "
                + $"-o \"{output}\"";

            var startInfo = new ProcessStartInfo("dotnet", arguments)
            {
                WorkingDirectory = RavenBotFolder,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using (var process = Process.Start(startInfo))
            {
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    Log(stdout.Result + stderr.Result);
                    File.WriteAllText(Path.Combine(OutputFolder, "package-log.txt"), log.ToString());
                    throw new InvalidOperationException($"dotnet publish of {Path.GetFileName(project)} for {runtime} failed, see package-log.txt");
                }
            }

            var files = Directory.GetFiles(output, "*", SearchOption.AllDirectories)
                .Where(x => Path.GetExtension(x).ToLowerInvariant() != ".pdb")
                .ToDictionary(x => Path.Combine("RavenBot", Path.GetRelativePath(output, x)), x => x);

            Compress(archive, files);
        }

        private void WriteChecksums()
        {
            var lines = new List<string>();
            foreach (var file in Directory.GetFiles(OutputFolder, "*.7z").OrderBy(x => x))
            {
                using var stream = File.OpenRead(file);
                var hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
                lines.Add(hash + "  " + Path.GetFileName(file));
            }
            File.WriteAllLines(Path.Combine(OutputFolder, "SHA256SUMS.txt"), lines);
        }

        public string LogText => log.ToString();

        private void Log(string line)
        {
            log.AppendLine(line);
        }
    }
}
