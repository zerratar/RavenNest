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
        /// <summary>Battle pets (the game's docs/battle-pets-design.md): trained by taming, and a share of everything while a pet is out.</summary>
        Taming,

        
        Melee = 900, // Referring to Attack, Defense and Strength

        None = 999,
    }
}
