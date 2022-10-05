namespace RavenNest.Models
{
    public enum CraftItemResultStatus
    {
        Success,
        PartialSuccess,
        LevelTooLow,
        InsufficientResources,
        UncraftableItem,
        UnknownItem,
        Error
    }
}
