using System;

namespace RavenNest.Blazor.Discord.Models
{
    public record AcccountInfo(long Coins, long HalloweenTokens, long ChristmasTokens, string ErrorMessage);

    public record CharacterList(CharacterInfo[] Characters, string ErrorMessage);

    public record CharacterInfo(Guid Id, int Index, string Name, string Alias, string ParticipatingTwitchStream, string Training, string Island, double RestedTime,
        bool InDungeon, bool InRaid, bool InOnsen, string Destination, DateTime? EstimatedTimeForLevelUp, long? ExpPerHour, Stats[] Stats);

    public record Stats(string Name, int Level);
}
