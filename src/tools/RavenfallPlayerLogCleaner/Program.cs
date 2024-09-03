
namespace RavenfallPlayerLogCleaner
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Auto delete ravenfall's player.log and player-prev.log when they exceed 10mb. Press Q to quit.");
            var cleaner = new LogCleaner();
            cleaner.Start();
            while (true)
            {
                if (System.Console.KeyAvailable)
                {
                    var key = System.Console.ReadKey();
                    if (key.Key == System.ConsoleKey.Q)
                    {
                        cleaner.Stop();
                        break;
                    }
                }
            }
        }
    }

    public class LogCleaner
    {
        private bool running;
        private Thread cleanerThread;

        private string playerLogPath;

        private FileInfo playerLog;
        private DateTime lastCheck;
        private const int minutes = 30;

        public LogCleaner()
        {
            cleanerThread = new System.Threading.Thread(CleanupProcess);

            // Get app_data folder
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            // locallow\shinobytes\Ravenfall Legacy\
            var ravenfallFolder = Path.Combine(appData, "..\\LocalLow\\shinobytes\\Ravenfall Legacy");

            playerLogPath = Path.Combine(ravenfallFolder, "Player.log");
            playerLog = new FileInfo(playerLogPath);
        }

        public void Clean()
        {
            var now = DateTime.UtcNow;
            if (now - lastCheck > TimeSpan.FromMinutes(minutes))
            {
                lastCheck = DateTime.UtcNow;

                var mb10 = 1024 * 1024 * 10;

                playerLog.Refresh();
                if (playerLog.Exists && playerLog.Length > mb10)
                {
                    Console.WriteLine("Player.log exceeded 10mb. Attempt to truncate.");
                    using (var str = playerLog.OpenWrite())
                    {
                        str.SetLength(0);
                    }
                }
            }
        }

        private void CleanupProcess()
        {
            try
            {
                while (running)
                {
                    try
                    {
                        Clean();
                        Thread.Sleep(1000);
                    }
                    catch (Exception exc)
                    {
                        Console.WriteLine("Could not truncate player.log: " + exc.Message.ToString() + ", will try again in " + minutes + " minutes.");
                        // ignored most of it it.
                    }
                }
            }
            catch
            {
            }
            running = false;
        }

        public void Start()
        {
            if (running) return;
            running = true;
            cleanerThread.Start();
        }

        public void Stop()
        {
            running = false;
        }
    }
}
