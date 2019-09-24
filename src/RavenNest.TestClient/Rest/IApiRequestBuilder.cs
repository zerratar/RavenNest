namespace RavenNest.TestClient.Rest
{
    public interface IApiRequestBuilder
    {
        IApiRequestBuilder Identifier(string value);
        IApiRequestBuilder AddParameter(string value);
        IApiRequestBuilder AddParameter(string key, object value);
        IApiRequestBuilder Method(string item);
        IApiRequest Build();
    }
}
