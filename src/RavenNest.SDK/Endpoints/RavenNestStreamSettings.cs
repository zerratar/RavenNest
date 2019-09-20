namespace RavenNest.SDK.Endpoints
{
    public class RavenNestStreamSettings : IAppSettings
    {
        public string ApiEndpoint => "https://www.ravenfall.stream/api/";
    }

    public class LocalRavenNestStreamSettings : IAppSettings
    {
        public string ApiEndpoint => "https://localhost:5001/api/";
    }
}