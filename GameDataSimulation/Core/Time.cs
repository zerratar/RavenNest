namespace GameDataSimulation
{
    public class Time
    {
        private static DateTime time = DateTime.UtcNow;
        private static DateTime realTime;
        public static DateTime UtcNow => time;
        public static DateTime Now => time;
        public static double Scale;
        internal static void Reset()
        {
            time = DateTime.UtcNow;
            realTime = time;
        }
        internal static void Update()
        {
            var now = DateTime.UtcNow;
            var elapsed = now - realTime;
            time = time.Add(elapsed * Scale);
            realTime = now;
        }
    }
}
