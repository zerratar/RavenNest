using Newtonsoft.Json;

namespace RavenNest.PatreonAPI.Models
{
    public class UserData
    {
        [JsonProperty(PropertyName = "data")]
        public User User { get; set; }

        [JsonProperty(PropertyName = "links")]
        public Links Links { get; set; }
    }
}
