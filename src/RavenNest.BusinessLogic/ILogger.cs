using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace RavenNest.BusinessLogic
{   
    public interface ILogger
    {
        Task WriteDebugAsync(string msg);
        Task WriteErrorAsync(string msg);
        Task WriteMessageAsync(string msg);
        Task WriteWarningAsync(string msg);
        void WriteDebug(string msg);
        void WriteError(string msg);
        void WriteMessage(string msg);
        void WriteWarning(string msg);
    }
}
