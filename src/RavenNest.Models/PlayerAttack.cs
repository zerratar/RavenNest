namespace RavenNest.Models
{
    public class PlayerAttack
    {
        public string UserId { get; set; }
        public string TargetId { get; set; }
        public int AttackType { get; set; }
    }
}