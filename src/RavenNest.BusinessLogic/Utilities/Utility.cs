using System;
using System.Globalization;
using System.Linq;

namespace RavenNest.BusinessLogic.Game
{
    public static class Utility
    {
        private readonly static char[] DescriberCharacters = new[] { 'a', 'i', 'o', 'u', 'e' };
        private readonly static string[] ExpValuePostfix = new string[] { " ", "k", "M", "G", "T", "P", "E", "Z", "Y", "R", "Q" };
        private readonly static string[] AmountPostFix = new string[] { "", "K", "M", "B", "T", "Q" };

        private static readonly Random random = new Random();

        public static T Random<T>()
            where T : struct, IConvertible
        {
            return Enum
                .GetValues(typeof(T)).Cast<T>()
                .OrderBy(x => random.NextDouble()).First();
        }

        public static int Random(int min, int max)
        {
            return random.Next(min, max);
        }

        public static string FormatTime(TimeSpan time)
        {
            if (time.TotalSeconds < 60) return time.TotalSeconds + " seconds";
            if (time.TotalMinutes < 60)
            {
                if (time.Seconds > 0)
                {
                    return time.Minutes + " minutes, " + time.Seconds + " seconds";
                }
                return time.Minutes + " minutes";
            }

            if (time.TotalDays > 1)
            {
                if (time.Hours > 0)
                {
                    return $"{time.Days} days, {time.Hours} hours";
                }

                return $"{time.Days} days";
            }

            if (time.Minutes > 0)
            {
                return $"{time.Hours} hours, {time.Minutes} minutes";
            }

            return $"{time.Hours} hours";
        }
        public static string FormatAmount(double value)
        {
            return FormatValue(value, AmountPostFix, "");
        }

        public static string FormatExp(double value)
        {
            return FormatValue(value, AmountPostFix, "");//ExpValuePostfix);
        }

        public static string FormatValue(double value, string[] postfix, string secondary = "Q")
        {
            var thousands = 0;
            while (value > 1000)
            {
                value = (value / 1000);
                thousands++;
            }

            if (thousands == 0)
            {
                return ((long)Math.Round(value, 1)).ToString(CultureInfo.InvariantCulture);
            }
            var pLen = postfix.Length - 1;
            var p0 = ((thousands - 1) % pLen) + 1;
            var q = thousands >= pLen ? secondary : "";
            return Math.Round(value, 1).ToString(CultureInfo.InvariantCulture) + postfix[0] + postfix[p0] + q;
        }

    }
}
