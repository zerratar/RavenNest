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
            if (time.TotalSeconds < 60) return time.TotalSeconds + "s";
            if (time.TotalMinutes < 60)
            {
                if (time.Seconds > 0)
                {
                    return time.Minutes + "m, " + time.Seconds + "s";
                }
                return time.Minutes + "m";
            }

            if (time.TotalDays > 1)
            {
                if (time.Hours > 0)
                {
                    return $"{time.Days}d, {time.Hours}h";
                }

                return $"{time.Days}d";
            }

            if (time.Minutes > 0)
            {
                return $"{time.Hours}h, {time.Minutes}m";
            }

            return $"{time.Hours}h";
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

        public static string SanitizeUserName(string displayName, string? fallback = null)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                return fallback ?? string.Empty;

            // Remove non-printable and surrogate characters (e.g., emojis)
            //var sanitized = new string(displayName
            //    .Where(c => !char.IsControl(c) && !char.IsSurrogate(c))
            //    .ToArray());

            var sanitized = new string(displayName
                .Where(c =>
                    (c >= 'a' && c <= 'z') ||
                    (c >= 'A' && c <= 'Z') ||
                    (c >= '0' && c <= '9') ||
                    c == ' ' || c == '_' || c == '-' || c == '.' || c == ',')
                .ToArray());

            // Trim whitespace
            sanitized = sanitized.Trim();

            const int MaxLength = 32;
            if (sanitized.Length > MaxLength)
                sanitized = sanitized.Substring(0, MaxLength);

            // If still empty, use fallback
            if (string.IsNullOrWhiteSpace(sanitized))
                return fallback ?? string.Empty;

            return sanitized;
        }
    }
}
