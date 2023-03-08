using System;

namespace RavenNest.Models
{
    public class PlayerAttack
    {
        public Guid PlayerId { get; set; }
        public Guid TargetId { get; set; }
        public int AttackType { get; set; }
    }
}
