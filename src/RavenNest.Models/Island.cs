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

        // none usually is 0, but we have already used it for ferry as it is a list of locations the player is located at. Not available places
        None = 254,
        Any = 255
    }
}
