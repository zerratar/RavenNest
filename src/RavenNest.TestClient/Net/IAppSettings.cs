namespace RavenNest.TestClient
{
    public interface IAppSettings
    {
        string ApiEndpoint { get; }
        string WebSocketEndpoint { get; }
    }
}
