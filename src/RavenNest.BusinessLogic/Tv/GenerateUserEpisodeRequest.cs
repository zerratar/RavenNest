using RavenNest.Models.Tv;
using System;

namespace RavenNest.BusinessLogic.Tv
{
    public class GenerateUserEpisodeRequest
    {
        public GenerateEpisodeRequest Request { get; set; }
        public Guid UserId { get; set; }
        public DateTime Created { get; set; }
    }
}
