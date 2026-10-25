namespace RavenNest.DataModels
{
    public enum Island : byte
    {
        Ferry = 0,
        Home = 1,
        Away = 2,
        Ironhill = 3,
        Kyo = 4,
        Heim = 5,
        Atria = 6,
        Eldara = 7,

        /// <summary>The mangrove island: opened per stream by the streamer's story "Insufficient bananas" (island_mangrove).</summary>
        Mangrove = 8,

        None = 254,
        Any = 255
    }
}
