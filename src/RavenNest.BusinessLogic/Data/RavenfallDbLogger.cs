using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;

namespace RavenNest.BusinessLogic
{
    public class RavenfallDbLogWriter
    {
        private readonly IRavenfallDbContextProvider dbProvider;

        public RavenfallDbLogWriter(IRavenfallDbContextProvider dbProvider)
        {
            this.dbProvider = dbProvider;

            // note(zerratar): Experimental. Trying Telepathy to avoid writing a 10th server/client connection again.
            //                 I'm too lazy at this point. The source code looks straight forward.

#pragma warning disable 4014
            Telepathy.Log.Info = str => WriteAsync(str, ServerLogSeverity.Message, "Telepathy");
            Telepathy.Log.Warning = str => WriteAsync(str, ServerLogSeverity.Warning, "Telepathy");
            Telepathy.Log.Error = str => WriteAsync(str, ServerLogSeverity.Error, "Telepathy");
#pragma warning restore 4014
        }

        public async Task WriteAsync(string msg, ServerLogSeverity severity, string categoryName, Exception exception = null)
        {
            try
            {
                var isKestrelError = categoryName.Contains("Microsoft.AspNetCore.Server.Kestrel", StringComparison.OrdinalIgnoreCase);
                // fugly hack, ignore circuit host errors
                // and exceptionahandlemiddlerware. these are just spamming the db with no interesting information.
                if (msg.Contains("CookiePreferencesChanged") ||
                    (msg.Contains("Unhandled exception while processing ") && isKestrelError) ||
                    (msg.Contains("Unexpected exception in TimingPipeFlusher.FlushAsync.") && isKestrelError) ||
                    categoryName.Contains("Microsoft.AspNetCore.Components.Server.Circuits.CircuitHost", StringComparison.OrdinalIgnoreCase) ||
                    categoryName.Contains("Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware", StringComparison.OrdinalIgnoreCase))
                {
                    await Console.Error.WriteLineAsync(msg + " [" + categoryName + "]");
                    return;
                }

                // log to file
                var logsDir = FolderPaths.LogsPath;//@"G:\Ravenfall\Data\generated-data\logs";
                if (!Directory.Exists(logsDir))
                {
                    Directory.CreateDirectory(logsDir);
                }

                try
                {
                    var logFile = Path.Combine(logsDir, categoryName + ".log");
                    System.IO.File.AppendAllText(logFile, "[" + DateTime.UtcNow.ToString("u") + "] <" + severity + "> " + msg.Trim() + $". {exception}\n");
                }
                catch
                {
                    // ignored
                }

                if (severity > ServerLogSeverity.Debug)
                {
                    using (var db = dbProvider.Get())
                    {
                        db.ServerLogs.Add(new ServerLogs
                        {
                            Created = DateTime.UtcNow,
                            Data = msg.Trim() + " [" + categoryName + "]",
                            Severity = severity
                        });
                        await db.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync(ex.Message);
            }
        }
    }

    public class RavenfallDbLogger : ILogger
    {
        private readonly string categoryName;
        private readonly RavenfallDbLogWriter dbLogWriter;

        public RavenfallDbLogger(string categoryName, RavenfallDbLogWriter dbLogWriter)
        {
            this.categoryName = categoryName;
            this.dbLogWriter = dbLogWriter;
        }

        private static readonly Dictionary<LogLevel, ServerLogSeverity> logLevelSeverityMapping = new Dictionary<LogLevel, ServerLogSeverity>
        {
            { LogLevel.None, ServerLogSeverity.Debug },
            { LogLevel.Trace, ServerLogSeverity.Debug },
            { LogLevel.Debug, ServerLogSeverity.Debug },
            { LogLevel.Information, ServerLogSeverity.Message },
            { LogLevel.Warning, ServerLogSeverity.Warning },
            { LogLevel.Error, ServerLogSeverity.Error },
            { LogLevel.Critical, ServerLogSeverity.Error },
        };

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            var message = formatter != null ? formatter(state, exception) : state.ToString();

#pragma warning disable 4014
            // used explicitly to not block a synchronous call 
            dbLogWriter.WriteAsync(message, logLevelSeverityMapping[logLevel], categoryName, exception);
#pragma warning restore 4014
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public IDisposable BeginScope<TState>(TState state) => null;
    }

    public class RavenfallDbLoggerProvider : ILoggerProvider
    {
        private readonly RavenfallDbLogWriter ravenfallDbLogWriter;

        public RavenfallDbLoggerProvider(IRavenfallDbContextProvider dbProvider)
        {
            ravenfallDbLogWriter = new RavenfallDbLogWriter(dbProvider);
        }

        public void Dispose() { }

        public ILogger CreateLogger(string categoryName) => new RavenfallDbLogger(categoryName, ravenfallDbLogWriter);
    }
}
