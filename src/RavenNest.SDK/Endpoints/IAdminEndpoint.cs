using System.Threading.Tasks;

namespace RavenNest.SDK.Endpoints
{
    public interface IAdminEndpoint
    {
        Task<byte[]> DownloadBackupAsync();
    }
}
