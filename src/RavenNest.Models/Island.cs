namespace RavenNest.Models
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

        /// <summary>Ravebridge, far to the west: an event island the streamer opens with the story "the road west".</summary>
        Ravebridge = 9,
        /// <summary>Out at sea on an own boat at anchor, fishing over the side (docs/own-boats.md in the game).</summary>
        Sea = 10,

        // none usually is 0, but we have already used it for ferry as it is a list of locations the player is located at. Not available places
        None = 254,
        Any = 255
    }
}
