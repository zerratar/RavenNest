namespace RavenNest
{
    public class PlayerDefinition
    {
        public PlayerDefinition(
            string userId,
            string name,
            CombatStats combatStats,
            SkillStats skillStats,
            SkillResources skillResources,
            Statistics statistics,
            PlayerAppearanceDefinition appearance,
            InventoryDefinition inventory)
        {
            this.UserId = userId;
            this.Name = name;
            this.CombatStats = combatStats ?? new CombatStats();
            this.SkillStats = skillStats ?? new SkillStats();
            this.SkillResources = skillResources ?? new SkillResources();
            this.Statistics = statistics ?? new Statistics();
            this.Appearance = appearance;
            this.Inventory = inventory ?? new InventoryDefinition(null, null);
        }

        public string UserId { get; set; }
        public string Name { get; }
        public CombatStats CombatStats { get; }
        public SkillStats SkillStats { get; }
        public SkillResources SkillResources { get; }
        public Statistics Statistics { get; }
        public PlayerAppearanceDefinition Appearance { get; }
        public InventoryDefinition Inventory { get; }
    }
}
