using Shinobytes.Console.Forms;
using System;
using System.Threading.Tasks;

namespace RavenNest.Tools.Actions
{
    /// <summary>The Tools menu entry (F2) for <see cref="ReleasePackager"/>: builds every file of a GitHub release.</summary>
    public class BuildUpdatePackageAction
    {
        private bool running;

        public BuildUpdatePackageAction(
          ProgressBar toolProgress,
          TextBlock toolStatus)
        {
            this.ToolProgress = toolProgress;
            this.ToolStatus = toolStatus;
        }

        public ProgressBar ToolProgress { get; }
        public TextBlock ToolStatus { get; }

        /// <summary>The version the release builds carry, or null when they are missing or not fit for release.</summary>
        public string GetVersion()
        {
            try
            {
                return new ReleasePackager(null, null).CheckBuilds();
            }
            catch
            {
                return null;
            }
        }

        public async void Apply()
        {
            if (running)
                return;

            running = true;
            ToolProgress.Indeterminate = false;
            ToolProgress.MaxValue = 100;
            var packager = new ReleasePackager(x => ToolStatus.Text = x, x => ToolProgress.Value = x);
            try
            {
                await Task.Run(() => packager.Run());
            }
            catch (Exception exc)
            {
                ToolStatus.Text = "Packaging failed: " + exc.Message;
            }
            finally
            {
                running = false;
            }
        }
    }
}
