using System;

namespace RavenNest.Models
{
    public class UpdateData
    {
        public string DownloadUrl { get; set; }
        public string Version { get; set; }
        public bool IsAlpha => Version?.IndexOf("a", StringComparison.OrdinalIgnoreCase) >= 0;
        public bool IsBeta => Version?.IndexOf("b", StringComparison.OrdinalIgnoreCase) >= 0;
        public DateTime Released { get; set; }
        public CodeOfConduct CodeOfConduct { get; set; }
    }

    public class CodeOfConduct
    {
        public string Title { get; set; }
        public string Message { get; set; }
        public DateTime LastModified { get; set; }
        public int Revision { get; set; }
        public bool VisibleInClient { get; set; }
    }
}
