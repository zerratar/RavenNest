namespace RavenNest.Models
{
    public enum ItemTradeState
    {
        DoesNotExist,
        DoesNotOwn,
        InsufficientCoins,
        RequestToLow,
        Success,
        Failed
    }
}