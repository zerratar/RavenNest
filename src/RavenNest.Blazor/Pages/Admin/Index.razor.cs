using Blazorise.Charts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Pages.Admin
{
    public partial class Index
    {
        private Models.SessionInfo session;
        private bool isAdmin;

        /// <summary>How many streams the table lists before it says how many it left out.</summary>
        private const int VisibleStreams = 10;

        private RavenNest.Blazor.Services.ServerOverview overview;
        private System.Threading.Timer refreshTimer;

        private sealed record Alert(string Title, string Detail, bool Severe);

        /// <summary>
        ///     Things an administrator would want to be interrupted by. Deliberately only produced
        ///     when true: a panel that always says everything is fine is a panel you stop reading,
        ///     and then it is not a warning any more.
        /// </summary>
        private List<Alert> Alerts
        {
            get
            {
                var alerts = new List<Alert>();
                if (overview == null) return alerts;

                if (!overview.BotOnline)
                {
                    var last = overview.BotLastUpdate == default
                        ? "It has not reported in at all."
                        : "Last heard from " + FormatSpan(DateTime.UtcNow - overview.BotLastUpdate) + " ago.";
                    alerts.Add(new Alert(
                        "The bot is not responding",
                        "It posts its details every few seconds, so a minute of silence counts as gone. " +
                        last + " Chat commands are not reaching the game.",
                        true));
                }
                else if (overview.BotChannelCount == 0 && overview.StreamCount > 0)
                {
                    alerts.Add(new Alert(
                        "The bot is online but in no channels",
                        overview.StreamCount + " stream(s) are running, so it should be in at least that many. " +
                        "Commands will not be reaching any of them.",
                        true));
                }

                if (overview.StreamCount > 0 && overview.PlayersInGame == 0)
                {
                    alerts.Add(new Alert(
                        "Streams are running with nobody playing",
                        overview.StreamCount + " session(s) are active and no characters are joined to any of them. " +
                        "Normal just after a stream starts, odd if it lasts.",
                        false));
                }

                return alerts;
            }
        }

        private ChartTimeFrame newUserTimeframe = ChartTimeFrame.ThisMonth;

        private ChartData<double> newUserChartData;
        private LineChartOptions newUserChartOptions;

        public ChartTimeFrame[] TimeFrames => Enum.GetValues<ChartTimeFrame>();

        // The chart library takes colour strings rather than CSS, so this is the one place a value
        // from ravenfall-tokens.css has to be repeated by hand. It is --rf-gold.
        private const string ChartGold = "#e8a33d";

        private int[] newUserSeries = Array.Empty<int>();
        private List<string> newUserLabels = new();

        private int NewUserTotal => newUserSeries.Length == 0 ? 0 : newUserSeries.Sum();

        private int BestPeriodValue => newUserSeries.Length == 0 ? 0 : newUserSeries.Max();

        private string BestPeriodLabel
        {
            get
            {
                if (newUserSeries.Length == 0 || newUserSeries.Max() == 0) return null;
                var index = Array.IndexOf(newUserSeries, newUserSeries.Max());
                return index >= 0 && index < newUserLabels.Count ? newUserLabels[index] : null;
            }
        }

        protected override async Task OnInitializedAsync()
        {
            session = AuthService.GetSession();
            isAdmin = session != null && session.Administrator;

            if (!isAdmin) return;

            overview = ServerService.GetServerOverview();
            await SelectTimeFrameAsync(ChartTimeFrame.ThisMonth);

            // Only the live half is polled. The signup chart walks every account, so re-running it
            // every ten seconds would cost far more than the number it produces is worth.
            refreshTimer = new System.Threading.Timer(_ =>
            {
                InvokeAsync(() =>
                {
                    overview = ServerService.GetServerOverview();
                    StateHasChanged();
                });
            }, null, 10000, 10000);
        }

        public void Dispose()
        {
            refreshTimer?.Dispose();
        }

        /// <summary>
        ///     Invariant culture, or a width written "43,2%" under a Swedish locale renders empty.
        /// </summary>
        private static string Bar(double progress)
        {
            var clamped = Math.Clamp(progress * 100d, 0d, 100d);
            return "width: " + clamped.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "%";
        }

        private static string FormatSpan(TimeSpan span)
        {
            if (span <= TimeSpan.Zero) return "0m";
            if (span.TotalDays >= 1) return (int)span.TotalDays + "d " + span.Hours + "h";
            if (span.TotalHours >= 1) return (int)span.TotalHours + "h " + span.Minutes + "m";
            return Math.Max(1, (int)span.TotalMinutes) + "m";
        }

        private async Task SelectTimeFrameAsync(ChartTimeFrame tf)
        {
            newUserTimeframe = tf;

            var start = GetStartTime(tf);
            var now = DateTime.UtcNow;
            var labels = GetChartLabels(tf);

            // Counted from accounts rather than characters. UserService.GetSignupDatesAsync records
            // what the old source could not see.
            var signups = await UserService.GetSignupDatesAsync(start, now);
            newUserSeries = GetChartData(signups, start, labels.Count, tf);
            newUserLabels = labels;

            newUserChartData ??= new ChartData<double>();
            newUserChartData.Labels ??= new List<object>();
            newUserChartData.Labels.Clear();
            foreach (var label in labels)
            {
                newUserChartData.Labels.Add(label);
            }

            var dataset = new LineChartDataset<double>
            {
                Label = "New accounts",
                Data = newUserSeries.Select(x => (double)x).ToList(),
                Fill = false,
                BorderColor = ChartGold,
                PointBorderColor = ChartGold,
                PointBackgroundColor = ChartGold,
                PointBorderWidth = 1,
                PointHoverRadius = 5,
                PointHoverBackgroundColor = ChartGold,
                PointHoverBorderColor = ChartGold,
                PointHoverBorderWidth = 2,
                PointRadius = 1,
                PointHitRadius = 10
            };

            newUserChartData.Datasets ??= new List<ChartDataset<double>>();
            newUserChartData.Datasets.Clear();
            newUserChartData.Datasets.Add(dataset);

            // MaintainAspectRatio off so the fixed height on .rf-chart--short decides how tall this
            // is. Left on, the chart claims most of a screen to draw one line.
            newUserChartOptions = new LineChartOptions
            {
                Responsive = true,
                MaintainAspectRatio = false,
                Scales = new ChartScales
                {
                    X = new ChartAxis
                    {
                        Title = new ChartScaleTitle { Text = new IndexableOption<string>(GetLabel(tf)) }
                    },
                    Y = new ChartAxis
                    {
                        Title = new ChartScaleTitle { Text = new IndexableOption<string>("Accounts") }
                    }
                }
            };

            await InvokeAsync(StateHasChanged);
        }

        private List<string> GetChartLabels(ChartTimeFrame tf)
        {
            var start = GetStartTime(tf);
            var steps = GetStepCount(start, tf);

            var output = new List<string>();
            for (var i = 0; i < steps; ++i)
            {
                output.Add(GetChartLabel(i, start, tf));
            }
            return output;
        }

        /// <summary>
        ///     Buckets creation dates into the steps a timeframe defines.
        /// </summary>
        /// <remarks>
        ///     The day cases offset from the window start rather than matching on the day number.
        ///     Matching on the number was wrong for Last month, whose window spans two calendar
        ///     months, so the fifth of either answered to the same bucket and whichever the group
        ///     happened to yield first won.
        /// </remarks>
        private static int[] GetChartData(
            IReadOnlyList<DateTime> source, DateTime start, int steps, ChartTimeFrame tf)
        {
            var output = new int[Math.Max(0, steps)];
            if (output.Length == 0) return output;

            foreach (var created in source)
            {
                int index;
                switch (tf)
                {
                    case ChartTimeFrame.LastSixMonths:
                    case ChartTimeFrame.LastThreeMonths:
                        index = ((created.Year - start.Year) * 12) + created.Month - start.Month;
                        break;
                    case ChartTimeFrame.LastMonth:
                    case ChartTimeFrame.ThisMonth:
                        index = (int)(created.Date - start.Date).TotalDays;
                        break;
                    default:
                        index = (int)(created - start).TotalHours;
                        break;
                }

                if (index >= 0 && index < output.Length)
                {
                    output[index]++;
                }
            }

            return output;
        }

        public string GetLabel(ChartTimeFrame frame)
        {
            switch (frame)
            {
                case ChartTimeFrame.LastMonth:
                case ChartTimeFrame.ThisMonth:
                    return "Day";
                case ChartTimeFrame.Today: return "Hour";
                default: return "Month";
            }
        }

        public string GetName(ChartTimeFrame frame)
        {
            var n = frame.ToString();
            var s = "";
            for (var i = 0; i < n.Length; ++i)
            {
                if (i == 0 || !Char.IsUpper(n[i])) s += n[i];
                else s += " " + n[i];
            }
            return s;
        }

        private static DateTime GetStartTime(ChartTimeFrame tf)
        {
            var now = DateTime.UtcNow;
            switch (tf)
            {
                case ChartTimeFrame.LastSixMonths: return new DateTime(now.Year, now.Month, 1).AddMonths(-6);
                case ChartTimeFrame.LastThreeMonths: return new DateTime(now.Year, now.Month, 1).AddMonths(-3);
                case ChartTimeFrame.LastMonth: return new DateTime(now.Year, now.Month, 1).AddMonths(-1);
                case ChartTimeFrame.ThisMonth: return new DateTime(now.Year, now.Month, 1);
                default: return now.Date;
            }
        }

        /// <summary>
        ///     Steps include the period in progress, so accounts created today have a bucket to go
        ///     in. Truncating the span left the current day or month off the end of the chart.
        /// </summary>
        private static int GetStepCount(DateTime start, ChartTimeFrame tf)
        {
            var now = DateTime.UtcNow;
            switch (tf)
            {
                case ChartTimeFrame.LastSixMonths:
                case ChartTimeFrame.LastThreeMonths:
                    return ((now.Year - start.Year) * 12) + now.Month - start.Month + 1;
                case ChartTimeFrame.LastMonth:
                    return DateTime.DaysInMonth(start.Year, start.Month);
                case ChartTimeFrame.ThisMonth:
                    return (int)(now.Date - start.Date).TotalDays + 1;
                default:
                    return (int)(now - start).TotalHours + 1;
            }
        }

        private static string GetChartLabel(int step, DateTime start, ChartTimeFrame tf)
        {
            switch (tf)
            {
                case ChartTimeFrame.LastSixMonths:
                case ChartTimeFrame.LastThreeMonths:
                    return start.Date.AddMonths(step).ToString("Y");
                case ChartTimeFrame.LastMonth:
                case ChartTimeFrame.ThisMonth:
                    return start.AddDays(step).ToString("d");
                default:
                    return start.AddHours(step).ToString("HH:mm");
            }
        }

        public enum ChartTimeFrame
        {
            LastSixMonths,
            LastThreeMonths,
            LastMonth,
            ThisMonth,
            Today
        }
    }
}
