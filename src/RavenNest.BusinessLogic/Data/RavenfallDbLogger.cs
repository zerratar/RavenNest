using System;
using System.Collections.Generic;
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
        }

        public void Write(string msg, ServerLogSeverity severity, string categoryName)
        {
            using (var db = dbProvider.Get())
            {
                db.ServerLogs.Add(new ServerLogs
                {
                    Created = DateTime.UtcNow,
                    Data = msg + " [" + categoryName + "]",
                    Severity = severity
                });
                db.SaveChanges();
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
            dbLogWriter.Write(message, logLevelSeverityMapping[logLevel], categoryName);
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
