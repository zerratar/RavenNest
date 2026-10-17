using Newtonsoft.Json.Linq;

namespace RavenNest.LoadTest.Rest;

public static class JsonExtensions
{
    /// <summary>A property by name, ignoring case: the server writes camelCase, the models are PascalCase.</summary>
    public static JToken? Get(this JToken? token, string name)
    {
        if (token is not JObject o) return null;
        var v = o.GetValue(name, StringComparison.OrdinalIgnoreCase);
        return v == null || v.Type == JTokenType.Null ? null : v;
    }

    public static string? Str(this JToken? token, string name) => token.Get(name)?.ToString();

    public static bool Bool(this JToken? token, string name) => token.Get(name)?.Type == JTokenType.Boolean && token.Get(name)!.Value<bool>();

    public static Guid GuidOf(this JToken? token, string name) => Guid.TryParse(token.Str(name), out var g) ? g : Guid.Empty;
}
