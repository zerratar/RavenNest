using System;
using System.Threading.Tasks;
using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;

namespace RavenNest.BusinessLogic
{
    public class RavenfallDbLogger : ILogger
    {
        private readonly IRavenfallDbContextProvider dbProvider;
        public RavenfallDbLogger(IRavenfallDbContextProvider dbProvider)
        {
            this.dbProvider = dbProvider;
        }
        public Task WriteDebugAsync(string msg) => WriteAsync(msg, ServerLogSeverity.Debug);
        public Task WriteErrorAsync(string msg) => WriteAsync(msg, ServerLogSeverity.Error);
        public Task WriteMessageAsync(string msg) => WriteAsync(msg, ServerLogSeverity.Message);
        public Task WriteWarningAsync(string msg) => WriteAsync(msg, ServerLogSeverity.Warning);
        public void WriteDebug(string msg) => Write(msg, ServerLogSeverity.Debug);
        public void WriteError(string msg) => Write(msg, ServerLogSeverity.Error);
        public void WriteMessage(string msg) => Write(msg, ServerLogSeverity.Message);
        public void WriteWarning(string msg) => Write(msg, ServerLogSeverity.Warning);
        private void Write(string msg, ServerLogSeverity severity)
        {
            using (var db = dbProvider.Get())
            {
                db.ServerLogs.Add(new ServerLogs
                {
                    Created = DateTime.UtcNow,
                    Data = msg,
                    Severity = severity
                });
                db.SaveChanges();
            }
        }
        private async Task WriteAsync(string msg, ServerLogSeverity severity)
        {
            using (var db = dbProvider.Get())
            {
                await db.ServerLogs.AddAsync(new ServerLogs
                {
                    Created = DateTime.UtcNow,
                    Data = msg,
                    Severity = severity
                });
                await db.SaveChangesAsync();
            }
        }
    }
}