namespace RavenNest.Models
{
    public enum Skill : int
    {
        Attack,
        Defense,
        Strength,
        Health,
        Woodcutting,
        Fishing,
        Mining,
        Crafting,
        Cooking,
        Farming,
        Slayer,
        Magic,
        Ranged,
        Sailing,
        Healing,
        Gathering,
        Alchemy,

        
        Melee = 900, // Referring to Attack, Defense and Strength
        None = 999,
    }
}
