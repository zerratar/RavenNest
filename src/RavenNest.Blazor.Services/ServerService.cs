using Microsoft.AspNetCore.Http;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.DataModels;
using RavenNest.Sessions;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text;

namespace RavenNest.Blazor.Services
{
    public class ServerService : RavenNestService
    {
        private readonly GameData gameData;
        private readonly IServerManager serverManager;

        public ServerService(
            GameData gameData,
            IServerManager serverManager,
            IHttpContextAccessor accessor,
            ISessionInfoProvider sessionInfoProvider)
            : base(accessor, sessionInfoProvider)
        {
            this.gameData = gameData;
            this.serverManager = serverManager;
        }

        public async Task<IReadOnlyList<RavenbotLogFile>> GetLogFilesAsync()
        {
            return await Task.Run(() =>
            {
                var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());
                var logsFolder = new DirectoryInfo(Path.Combine(currentDir.Parent.FullName, "logs"));

                if (!logsFolder.Exists)
                {
                    return new List<RavenbotLogFile>();
                }

                var result = new List<RavenbotLogFile>();

                foreach (var file in logsFolder.GetFiles("*.log").OrderByDescending(x => x.CreationTime))
                {
                    DateTime.TryParse(file.Name.Replace(".log", ""), out var date);
                    result.Add(new RavenbotLogFile
                    {
                        FileSize = file.Length,
                        FileName = file.Name,
                        Date = date,
                        DownloadUrl = "/api/admin/ravenbot-logs/" + file.Name
                    });
                }
                return result;
            });
        }

        public async Task<LogEntryCollection> GetRavenbotLogEntriesAsync(string file, int offset, int take)
        {
            var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());
            var logsFolder = new DirectoryInfo(Path.Combine(currentDir.Parent.FullName, "logs"));

            FileInfo fullFileNamePath = null;
            List<LogEntry> LogLines = new();

            if (logsFolder.Exists)
            {
                fullFileNamePath = new FileInfo(Path.Combine(logsFolder.FullName, file));
            }

            if (fullFileNamePath is null)
            {
                LogLines.Add(LogEntry.Error("Unable to find a logfile of name '" + file + "'."));
                return new LogEntryCollection { TotalCount = 1, LogEntries = LogLines, Offset = 0 };
            }
            var rowIndex = 0;
            var lastReadLine = "";
            try
            {
                using (var inStream = new FileStream(fullFileNamePath.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var read = new StreamReader(inStream))
                {
                    do
                    {
                        // Normally, we should be able to break out
                        // from here and don't continue reading at this point
                        // but we need to know the total amount of rows.
                        // So we will just skip adding to the list instead.
                        //if (LogLines.Count >= take)
                        //{
                        //    break;
                        //}

                        lastReadLine = await read.ReadLineAsync();

                        if (rowIndex++ < offset)
                        {
                            continue;
                        }

                        if (LogLines.Count >= take)
                        {
                            continue;
                        }

                        // check if its a legacy line. then parse it as such.
                        if (!string.IsNullOrEmpty(lastReadLine) && lastReadLine.StartsWith("["))
                        {
                            LogLines.Add(ParseLegacyLogLine(lastReadLine));
                            continue;
                        }

                        if (string.IsNullOrEmpty(lastReadLine))
                        {
                            continue;
                        }

                        LogLines.Add(JsonSerializer.Deserialize<LogEntry>(lastReadLine));
                    } while (!read.EndOfStream);
                }
            }
            catch (Exception exc)
            {
                if (!string.IsNullOrEmpty(lastReadLine))
                {
                    lastReadLine = " When parsing the following content '" + lastReadLine + "'";
                }

                LogLines.Add(LogEntry.Error("Reading log '" + file + "' threw an exception: " + exc.Message + lastReadLine));
            }

            return new LogEntryCollection
            {
                LogEntries = LogLines,
                Offset = offset,
                TotalCount = rowIndex
            };
        }

        private LogEntry ParseLegacyLogLine(string line)
        {
            // anything that happens in this function, let it throw an exception. It will be caught by GetRavenbotLogEntriesAsync
            var msg = line;
            var dateTime = DateTime.Parse(line.Substring(1, line.IndexOf(']') - 1));
            var logLevel = LogLevel.Information;

            var logLevelStart = line.IndexOf('{');

            if (logLevelStart >= 0)
            {
                var logLevelEnd = line.IndexOf('}');
                var logLevelValue = line.Substring(logLevelStart + 1, logLevelEnd - logLevelStart - 1);

                // Enum.TryParse<LogLevel> not supported with Span?

                if (logLevelValue.StartsWith("d", StringComparison.OrdinalIgnoreCase))
                    logLevel = LogLevel.Debug;
                if (logLevelValue.StartsWith("t", StringComparison.OrdinalIgnoreCase))
                    logLevel = LogLevel.Trace;
                if (logLevelValue.StartsWith("e", StringComparison.OrdinalIgnoreCase))
                    logLevel = LogLevel.Error;
                if (logLevelValue.StartsWith("c", StringComparison.OrdinalIgnoreCase))
                    logLevel = LogLevel.Critical;
                if (logLevelValue.StartsWith("w", StringComparison.OrdinalIgnoreCase))
                    logLevel = LogLevel.Warning;

                msg = msg.Substring(logLevelEnd + 1);
                if (msg.StartsWith(": "))
                {
                    msg = msg.Replace(": ", "");
                }
            }

            return new LogEntry
            {
                LogLevel = logLevel,
                Message = msg.Trim(),
                LogDateTime = dateTime,
            };
        }

        public BotStats GetBotStats()
        {
            return gameData.Bot;
        }

        public void SendServerAnnouncement(string message, int milliSeconds)
        {
            serverManager.BroadcastMessageAsync(message, milliSeconds);
        }

        public void SendExpMultiplierEvent(int multiplier, string message, DateTime? startTime, DateTime endTime)
        {
            serverManager.SendExpMultiplierEventAsync(multiplier, message, startTime, endTime);
        }

        public Task<bool> DeleteCodeOfConduct()
        {
            return Task.Run(() =>
            {
                var agreements = gameData.GetAllAgreements();
                var coc = agreements.FirstOrDefault(x => x.Type.ToLower() == "coc");
                if (coc != null)
                {
                    gameData.Remove(coc);
                    return true;
                }
                else
                {
                    return false;
                }
            });
        }


        public Task<Agreements> UpdateCodeOfConduct(UpdateCodeOfConduct data)
        {
            return Task.Run(() =>
            {
                var agreements = gameData.GetAllAgreements();
                var coc = agreements.FirstOrDefault(x => x.Type.ToLower() == "coc");
                if (coc != null)
                {
                    if (coc.Message != data.Message || coc.Title != data.Title || coc.VisibleInClient != data.VisibleInClient)
                    {
                        coc.Message = data.Message;
                        coc.Title = data.Title;
                        coc.Revision = coc.Revision + 1;
                        coc.LastModified = DateTime.UtcNow;
                        coc.VisibleInClient = data.VisibleInClient;
                    }
                }
                else
                {
                    coc = new Agreements
                    {
                        Id = Guid.NewGuid(),
                        Type = "coc",
                        Message = data.Message,
                        Title = data.Title,
                        Revision = 1,
                        ValidFrom = DateTime.UtcNow,
                        LastModified = DateTime.UtcNow,
                        VisibleInClient = data.VisibleInClient
                    };
                    gameData.Add(coc);
                }
                return coc;
            });
        }

        public Task<Agreements> GetCodeOfConductAsync()
        {
            return Task.Run(() =>
            {
                var agreements = gameData.GetAllAgreements();
                return agreements.FirstOrDefault(x => x.Type.ToLower() == "coc");
            });
        }
    }

    public class UpdateCodeOfConduct
    {
        public string Title { get; set; }
        public string Message { get; set; }
        public bool VisibleInClient { get; set; }
    }

    public class RavenbotLogFile
    {
        public string DownloadUrl { get; set; }
        public string FileName { get; set; }
        public DateTime Date { get; set; }
        public long FileSize { get; set; }
    }

    public class LogEntryCollection
    {
        public List<LogEntry> LogEntries { get; set; }
        public int Offset { get; set; }
        public int TotalCount { get; set; }
    }

    //TODO - Move to Model, code copy also exisit in PersistedConsoleLogger in RavenBot.ROBot
    public class LogEntry
    {
        public DateTime LogDateTime { get; set; }
        public LogLevel LogLevel { get; set; }
        public string Message { get; set; }

        public static LogEntry Error(string message)
        {
            return new LogEntry
            {
                LogLevel = LogLevel.Error,
                LogDateTime = DateTime.UtcNow,
                Message = message
            };
        }
    }
}
