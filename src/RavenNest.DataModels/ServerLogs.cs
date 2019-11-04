using System;
using System.Collections.Generic;
using System.Text;

namespace RavenNest.DataModels
{
    public enum ServerLogSeverity : int
    {
        Debug,
        Message,
        Warning,
        Error
    }

    public class ServerLogs
    {
        public long Id { get; set; }
        public ServerLogSeverity Severity { get; set; }
        public string Data { get; set; }
        public DateTime Created { get; set; }
    }
}
