using System;

namespace RavenNest
{
    [Serializable]
    public class CraftingRequirements
    {
        public int MinCraftingLevel { get; set; }
        public int MinCookingLevel { get; set; }
        public int Wood { get; set; }
        public int Ore { get; set; }
        public int Fish { get; set; }
        public int Wheat { get; set; }
    }
}
