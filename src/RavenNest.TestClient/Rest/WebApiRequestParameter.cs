namespace RavenNest.TestClient.Rest
{
    public class WebApiRequestParameter : IRequestParameter
    {
        public string Key { get; }
        public string Value { get; }

        public WebApiRequestParameter(string key, string value)
        {
            Value = value;
            Key = key;
        }
    }
}
