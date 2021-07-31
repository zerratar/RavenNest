namespace RavenNest.SDK.Endpoints
{
    public class UpdatePlayerExperience
    {
        public UpdatePlayerExperience(string userId, double[] experience)
        {
            UserId = userId;
            Experience = experience;
        }

        public string UserId { get; }
        public double[] Experience { get; }
    }
}
