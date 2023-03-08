using ChartJs.Blazor.Common.Axes;
using ChartJs.Blazor.LineChart;
using RavenNest.BusinessLogic.Extended;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Pages.Admin
{
    public partial class Index
    {
        private SessionInfo session;
        private bool isAdmin;

        private ChartTimeFrame newUserTimeframe = ChartTimeFrame.ThisMonth;
        private LineConfig newUserConfig;
        private LineConfig commonHoursNewUsers;

        public ChartTimeFrame[] TimeFrames => Enum.GetValues<ChartTimeFrame>();

        protected override async Task OnInitializedAsync()
        {
            session = AuthService.GetSession();
            isAdmin = session != null && session.Administrator;

            await SelectTimeFrameAsync(ChartTimeFrame.ThisMonth);
        }

        private async Task SelectTimeFrameAsync(ChartTimeFrame tf)
        {
            newUserTimeframe = tf;
            await CreateNewUserConfig(tf);
            await CreateNewUserConfig(tf, true);
            await InvokeAsync(StateHasChanged);
        }

        private async Task<LineConfig> CreateNewUserConfig(
          ChartTimeFrame tf,
          bool avgUserPerHour = false)
        {
            LineConfig config = null;
            if (!avgUserPerHour)
            {
                if (newUserConfig == null)
                    newUserConfig = new LineConfig();
                config = newUserConfig;
            }
            else
            {
                if (commonHoursNewUsers == null)
                    commonHoursNewUsers = new LineConfig();
                config = commonHoursNewUsers;
            }

            config.Options = new LineOptions()
            {
                Responsive = true,
                Title = new ChartJs.Blazor.Common.OptionsTitle()
                {
                    Display = true,
                    Text = avgUserPerHour ? "Users per hour" : "New users of " + GetName(tf)
                },
                Scales = new Scales
                {
                    XAxes = new List<CartesianAxis> {
                      new CategoryAxis
                      {
                        ScaleLabel = new ScaleLabel { LabelString = GetLabel(tf) }
                      }
                },
                    YAxes = new List<CartesianAxis> {
                      new LinearCartesianAxis
                      {
                        ScaleLabel = new ScaleLabel { LabelString = "Value" }
                      }
                    }
                }
            };

            return await UpdateDatasetAsync(config, tf, avgUserPerHour);
        }

        private List<string> GetChartLabels(ChartTimeFrame tf, bool avgUserPerHour)
        {
            var now = DateTime.UtcNow;
            var start = GetStartTime(tf, avgUserPerHour);
            var steps = GetStepCount(start, tf, avgUserPerHour);

            List<string> output = new List<string>();
            for (var i = 0; i < steps; ++i)
            {
                output.Add(GetChartLabel(i, steps, start, tf, avgUserPerHour));
            }
            return output;
        }

        private async Task<LineConfig> UpdateDatasetAsync(
          LineConfig config,
          ChartTimeFrame tf,
          bool avgUserPerHour)
        {
            var now = DateTime.UtcNow;
            var start = GetStartTime(tf, avgUserPerHour);
            var labels = GetChartLabels(tf, avgUserPerHour);

            config.Data.Labels.Clear();
            foreach (var lb in labels)
            {
                config.Data.Labels.Add(lb);
            }

            var userData = await UserService.GetUsersByCreatedAsync(start, now);
            var outputData = GetChartData(userData, start, labels.Count, tf, avgUserPerHour);
            var total = outputData.Length > 0 ? outputData.Sum() : 0;

            LineDataset<int> dataset = new ChartJs.Blazor.LineChart.LineDataset<int>(outputData)
            {
                Label = "New users (" + total + ")",
            };
            config.Data.Datasets.Clear();
            config.Data.Datasets.Add(dataset);

            try
            {
                if (string.IsNullOrEmpty(config?.Options?.Title?.Text?.SingleValue))
                {
                    return config;
                }

                var title = config?.Options?.Title?.Text?.SingleValue;
                if (outputData != null && outputData.Length > 0 && title != null)
                {
                    config.Options.Title.Text = title + " - Total " + total;
                }
            }
            catch { }
            return config;
        }

        public int[] GetChartData(
          IReadOnlyList<WebsiteAdminUser> source,
          DateTime start,
          int steps,
          ChartTimeFrame tf,
          bool avgUserPerHour)
        {
            var outputData = new int[steps];

            IEnumerable<IGrouping<DateTime, WebsiteAdminUser>> grouped = null;

            if (avgUserPerHour)
            {
                var data = source
                     .GroupBy(x => x.Created.Hour)
                     .OrderBy(x => x.Key)
                     .ToArray();

                for (var i = 0; i < outputData.Length; ++i)
                {
                    var record = data.FirstOrDefault(x => x.Key == i);
                    outputData[record.Key] = record?.Count() ?? 0;
                }
                return outputData;
            }

            switch (tf)
            {

                //case ChartTimeFrame.AllTime:
                //  {
                //      var d = data
                //        .GroupBy(x => new DateTime(x.Created.Date.Year, x.Created.Date.Month, 1))
                //        .OrderBy(x => x.Key)
                //        .ToArray();
                //    for (var i = 0; i < outputData.Length; ++i)
                //    {
                //        outputData[i] = d.
                //    }
                //  }
                //  break;

                case ChartTimeFrame.LastSixMonths:
                case ChartTimeFrame.LastThreeMonths:
                {
                    var records = source
                        .GroupBy(x => new DateTime(x.Created.Date.Year, x.Created.Date.Month, 1))
                        .OrderBy(x => x.Key)
                        .ToArray();
                    for (var i = 0; i < outputData.Length; ++i)
                    {
                        var t = start.AddMonths(i);
                        outputData[i] = records.FirstOrDefault(x => x.Key == t)?.Count() ?? 0;
                    }
                }
                break;
                case ChartTimeFrame.LastMonth:
                case ChartTimeFrame.ThisMonth:
                {
                    var records = source.GroupBy(x => x.Created.Date).ToArray();
                    for (var i = 0; i < outputData.Length; ++i)
                    {
                        outputData[i] = records.FirstOrDefault(x => x.Key.Day == i + 1)?.Count() ?? 0;
                    }
                }
                break;
                //case ChartTimeFrame.ThisWeek:
                //  var di = 0;
                //  foreach (var g in data.GroupBy(x => x.Created.Date).OrderBy(x => x.Key))
                //    outputData[++di] = g.Count();
                //  break;

                case ChartTimeFrame.Today:
                {
                    var records = source.GroupBy(x =>
                    {
                        var d = x.Created.Date;
                        var h = x.Created.TimeOfDay.Hours;
                        return d.AddHours(h);
                    });
                    for (var i = 0; i < outputData.Length; ++i)
                    {
                        outputData[i] = records.FirstOrDefault(x => x.Key.Hour == i)?.Count() ?? 0;
                    }
                }
                break;
            }

            return outputData;
        }

        public string GetLabel(ChartTimeFrame frame)
        {
            switch (frame)
            {
                case ChartTimeFrame.LastMonth:
                case ChartTimeFrame.ThisMonth:
                    //case ChartTimeFrame.ThisWeek:
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

        private DateTime GetStartTime(ChartTimeFrame tf, bool avgUserPerHour)
        {
            var now = DateTime.UtcNow;
            if (avgUserPerHour)
            {
                return DateTime.MinValue;
            }
            switch (tf)
            {
                //case ChartTimeFrame.AllTime: return DateTime.MinValue;
                case ChartTimeFrame.LastSixMonths: return new DateTime(now.Date.Year, now.Date.Month, 1).AddMonths(-6);
                case ChartTimeFrame.LastThreeMonths: return new DateTime(now.Date.Year, now.Date.Month, 1).AddMonths(-3);
                case ChartTimeFrame.LastMonth: return new DateTime(now.Date.Year, now.Date.Month, 1).AddMonths(-1);
                case ChartTimeFrame.ThisMonth: return new DateTime(now.Year, now.Month, 1);
                //case ChartTimeFrame.ThisWeek: return now.Date.AddDays(-7);
                default: return now.Date;
            }
        }

        private int GetStepCount(DateTime start, ChartTimeFrame tf, bool avgUserPerHour)
        {
            if (avgUserPerHour)
            {
                return 24;
            }

            var now = DateTime.UtcNow;
            var range = now - start;

            switch (tf)
            {
                //case ChartTimeFrame.AllTime: return MonthDifference(start, now);
                case ChartTimeFrame.LastSixMonths: return MonthDifference(start, now);
                case ChartTimeFrame.LastThreeMonths: return MonthDifference(start, now);
                case ChartTimeFrame.LastMonth: return (int)(new DateTime(now.Year, now.Month, 1) - start).TotalDays;
                case ChartTimeFrame.ThisMonth: return (int)range.TotalDays;
                //case ChartTimeFrame.ThisWeek: return (int)range.TotalDays;
                default: return (int)range.TotalHours;
            }
        }

        private string GetChartLabel(int step, int steps, DateTime start, ChartTimeFrame tf, bool avgUserPerHour)
        {
            if (avgUserPerHour)
            {
                return step.ToString("00");
            }

            switch (tf)
            {
                //case ChartTimeFrame.AllTime: return start.Date.AddMonths(step).ToString("Y");
                case ChartTimeFrame.LastSixMonths: return start.Date.AddMonths(step).ToString("Y");
                case ChartTimeFrame.LastThreeMonths: return start.Date.AddMonths(step).ToString("Y");
                case ChartTimeFrame.LastMonth: return start.AddDays(step).ToString("d");
                case ChartTimeFrame.ThisMonth: return start.AddDays(step).ToString("d");
                //case ChartTimeFrame.ThisWeek: return start.AddDays(step).ToString("d");
                default: return start.AddHours(step).ToString("HH:mm");
            }
        }

        public int MonthDifference(DateTime lValue, DateTime rValue)
        {
            return Math.Abs((lValue.Month - rValue.Month) + 12 * (lValue.Year - rValue.Year));
        }

        public enum ChartTimeFrame
        {
            //AllTime,
            LastSixMonths,
            LastThreeMonths,
            LastMonth,
            ThisMonth,
            //ThisWeek,
            Today
        }
    }
}
