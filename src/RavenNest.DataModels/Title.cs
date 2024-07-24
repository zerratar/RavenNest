namespace RavenNest.DataModels
{
    public partial class Title : Entity<Title>
    {
        private string name;
        private string description;
        private int bonusType; // e.g., 1 = Attack, 2 = Defense, etc.
        private double bonusValue; // e.g., 0.1 for 10% bonus

        public string Name { get => name; set => Set(ref name, value); }
        public string Description { get => description; set => Set(ref description, value); }
        public int BonusType { get => bonusType; set => Set(ref bonusType, value); }
        public double BonusValue { get => bonusValue; set => Set(ref bonusValue, value); }
    }
}
