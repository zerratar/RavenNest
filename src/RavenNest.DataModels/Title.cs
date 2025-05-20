using RavenNest.DataAnnotations;

namespace RavenNest.DataModels
{
    public partial class Title : Entity<Title>
    {
        [PersistentData] private string name;
        [PersistentData] private string description;
        [PersistentData] private int bonusType; // e.g., 1 = Attack, 2 = Defense, etc.
        [PersistentData] private double bonusValue; // e.g., 0.1 for 10% bonus
    }
}
