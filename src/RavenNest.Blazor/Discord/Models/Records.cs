﻿using Newtonsoft.Json;
using System;

namespace RavenNest.Blazor.Discord.Models
{
    public record AcccountInfo(long Coins, long HalloweenTokens, long ChristmasTokens, string ErrorMessage);

    public record CharacterList(CharacterInfo[] Characters, string ErrorMessage);

    public record CharacterInfo(
        Guid Id, int Index, string Name, string Alias, int combatLevel, string ParticipatingTwitchStream,
        string Training, string Island, double RestedTimeSeconds,
        bool InDungeon, bool InRaid, bool InOnsen, string Destination,
        DateTime? EstimatedTimeForLevelUp, long? ExpPerHour, Stats[] Stats, CharacterEquipment equipment);

    public record CharacterEquipment(
            double totalArmorPower, double totalWeaponAim, double totalWeaponPower,
            double totalRangedAim, double totalRangedPower, double totalMagicAim, double totalMagicPower,
            string[] equippedItemNames);

    public record Stats(string Name, int Level, long experience, long expForNextLevel, float progress);
}
