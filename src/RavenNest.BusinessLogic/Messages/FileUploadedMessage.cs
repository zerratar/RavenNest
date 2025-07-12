using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RavenNest.BusinessLogic.Messages
{
    public class FileUploadedMessage
    {
        public Guid RequestId { get; set; }
        public Guid UserId { get; set; }
        public Guid SessionId { get; set; }
        public string UserName { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
    }
}
