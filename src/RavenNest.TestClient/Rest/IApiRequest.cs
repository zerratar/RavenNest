using System.Threading.Tasks;

namespace RavenNest.TestClient.Rest
{
    public interface IApiRequest
    {
        Task<TResult> SendAsync<TResult, TModel>(ApiRequestTarget target, ApiRequestType type, TModel model);
        Task<TResult> SendAsync<TResult>(ApiRequestTarget target, ApiRequestType type);
        Task SendAsync(ApiRequestTarget target, ApiRequestType type);
    }
}
