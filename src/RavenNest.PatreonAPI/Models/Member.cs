using Newtonsoft.Json;

namespace RavenNest.PatreonAPI.Models
{
    public class Member
    {
        [JsonProperty(PropertyName = "type")]
        public string Type { get; set; }

        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "attributes")]
        public MemberAttributes Attributes { get; set; }

        [JsonProperty(PropertyName = "relationships")]
        public MemberRelationships Relationships { get; set; }
    }
}
