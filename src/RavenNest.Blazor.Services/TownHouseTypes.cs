using RavenNest.BusinessLogic.Net;
using System.Collections.Generic;
using System.Linq;

namespace RavenNest.Blazor.Services
{
    /// <summary>
    ///     One house type: what to call it, what to draw it with, and which skill decides its
    ///     bonus.
    /// </summary>
    /// <param name="SkillName">
    ///     The skill <see cref="TownService.GetSkillByHouseType"/> actually reads. It is the same
    ///     word as the type for every house but one: a Melee house is driven by the assignee's
    ///     Health level, which is not something the game says anywhere.
    /// </param>
    public sealed record TownHouseTypeOption(TownHouseSlotType Type, string Label, string Icon, string SkillName);

    /// <summary>
    ///     The house type vocabulary, in one place, in the order the game declares them.
    ///
    ///     Icons are the same FontAwesome choices the item filter rail already uses wherever the
    ///     two overlap, so a woodcutting house and the woodcutting item category do not end up
    ///     drawn with different trees.
    /// </summary>
    public static class TownHouseTypes
    {
        public static readonly TownHouseTypeOption[] All = new[]
        {
            new TownHouseTypeOption(TownHouseSlotType.Woodcutting, "Woodcutting", "fa-solid fa-tree", "Woodcutting"),
            new TownHouseTypeOption(TownHouseSlotType.Magic, "Magic", "fa-sharp fa-solid fa-staff", "Magic"),
            new TownHouseTypeOption(TownHouseSlotType.Ranged, "Ranged", "fa-sharp fa-solid fa-bow-arrow", "Ranged"),
            new TownHouseTypeOption(TownHouseSlotType.Melee, "Melee", "fa-sharp fa-solid fa-sword", "Health"),
            new TownHouseTypeOption(TownHouseSlotType.Cooking, "Cooking", "fa-sharp fa-solid fa-user-chef", "Cooking"),
            new TownHouseTypeOption(TownHouseSlotType.Crafting, "Crafting", "fa-sharp fa-solid fa-hammer", "Crafting"),
            new TownHouseTypeOption(TownHouseSlotType.Farming, "Farming", "fa-solid fa-wheat", "Farming"),
            new TownHouseTypeOption(TownHouseSlotType.Mining, "Mining", "fa-solid fa-pickaxe", "Mining"),
            new TownHouseTypeOption(TownHouseSlotType.Sailing, "Sailing", "fa-solid fa-sailboat", "Sailing"),
            new TownHouseTypeOption(TownHouseSlotType.Slayer, "Slayer", "fa-solid fa-skull", "Slayer"),
            new TownHouseTypeOption(TownHouseSlotType.Fishing, "Fishing", "fa-sharp fa-solid fa-fishing-rod", "Fishing"),
            new TownHouseTypeOption(TownHouseSlotType.Healing, "Healing", "fa-solid fa-heart", "Healing"),
            new TownHouseTypeOption(TownHouseSlotType.Gathering, "Gathering", "fa-solid fa-mushroom", "Gathering"),
            new TownHouseTypeOption(TownHouseSlotType.Alchemy, "Alchemy", "fa-sharp fa-solid fa-flask", "Alchemy"),
        };

        private static readonly Dictionary<TownHouseSlotType, TownHouseTypeOption> byType =
            All.ToDictionary(x => x.Type);

        /// <summary>
        ///     Undefined and NoSkill are not house types, they are an empty plot, and they return
        ///     null rather than a fourteenth entry nobody can build.
        /// </summary>
        public static TownHouseTypeOption Get(TownHouseSlotType type)
        {
            return byType.TryGetValue(type, out var option) ? option : null;
        }

        public static string LabelOf(TownHouseSlotType type) => Get(type)?.Label ?? "Empty plot";

        public static string IconOf(TownHouseSlotType type) => Get(type)?.Icon ?? "fa-regular fa-square-dashed";
    }
}
