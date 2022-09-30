namespace RavenNest.Models
{
    public class UseExpScrollResult
    {
        public ScrollUseResult Result { get; set; }
        public int Used { get; set; }
        public ExpMultiplier Multiplier { get; set; }

        public static UseExpScrollResult Error(ExpMultiplier newMultiplier) => new UseExpScrollResult
        {
            Multiplier = newMultiplier,
            Result = ScrollUseResult.Error,
            Used = -1
        };

        public static UseExpScrollResult InsufficientScrolls(ExpMultiplier newMultiplier) => new UseExpScrollResult
        {
            Multiplier = newMultiplier,
            Result = ScrollUseResult.InsufficientScrolls,
            Used = -2
        };

        public static UseExpScrollResult Success(int usageCount, ExpMultiplier newMultiplier) => new UseExpScrollResult
        {
            Result = ScrollUseResult.Success,
            Used = usageCount,
            Multiplier = newMultiplier
        };
    }
}
