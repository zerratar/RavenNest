using RavenNest.BusinessLogic;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Net;
using RavenNest.DataModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
namespace RavenNest.Blazor.Services
{
    public class TownService
    {
        private readonly GameData gameData;
        private const float MaxExpBonusPerSlot = 200f;

        /// <summary>
        ///     The player count VillageProcessor hands GameMath.GetVillageExperience. It is a
        ///     fixed rate there rather than the session's real population, and the estimate here
        ///     is only right while it stays that way.
        /// </summary>
        private const int VillageProcessorPlayerCount = 750;

        public TownService(GameData gameData)
        {
            this.gameData = gameData;
        }

        public async Task<IReadOnlyList<TownData>> GetTownsAsync()
        {
            return await Task.Run(() =>
            {
                var result = new List<TownData>();
                var activeSessions = this.gameData.GetActiveSessions();

                foreach (var sess in activeSessions)
                {
                    var village = gameData.GetVillageByUserId(sess.UserId);
                    if (village == null) continue;

                    var houses = this.gameData.GetOrCreateVillageHouses(village);//this.gameData.GetVillageHouses(village);
                    if (houses == null || houses.Count == 0)
                    {
                        continue;
                    }

                    var town = new TownData();
                    town.Id = village.Id;
                    town.Level = village.Level;
                    town.Experience = village.Experience;
                    town.Name = village.Name;

                    var owner = this.gameData.GetUser(village.UserId);
                    town.Owner = new TownOwnerData();
                    town.Owner.Id = owner?.Id ?? Guid.Empty;
                    town.Owner.UserName = owner?.UserName;

                    var tHouses = new List<TownHouseData>();
                    var intSlotCount = 0;
                    foreach (var house in houses)
                    {
                        ++intSlotCount;
                        if (house.UserId == null)
                        {
                            continue;
                        }

                        var h = new TownHouseData();
                        h.Id = house.Id;
                        h.Type = (TownHouseSlotType)house.Type;

                        var bestHouseSkill = new SkillStat();
                        var isActive = false;
                        if (house.CharacterId != null)
                        {
                            var targetCharacter = gameData.GetCharacter(house.CharacterId.Value);
                            if (targetCharacter != null)
                            {
                                var cs = gameData.GetCharacterSkills(targetCharacter.SkillsId);
                                var houseSkill = GetSkillByHouseType(cs, h.Type);
                                if (targetCharacter.UserIdLock == town.Owner.Id)
                                {
                                    isActive = true;
                                    bestHouseSkill = houseSkill;
                                    h.AssignedCharacterId = targetCharacter.Id;
                                    h.Bonus = CalculateHouseExpBonus(bestHouseSkill);
                                }
                            }
                        }
                        
                        if (!isActive)
                        {
                            var chars = gameData.GetCharactersByUserId(house.UserId.GetValueOrDefault());

                            if (chars != null && chars.Count > 0)
                            {
                                foreach (var c in chars)
                                {
                                    var cs = gameData.GetCharacterSkills(c.SkillsId);
                                    var houseSkill = GetSkillByHouseType(cs, h.Type);

                                    if (c.UserIdLock == town.Owner.Id)
                                    {
                                        isActive = true;
                                        bestHouseSkill = houseSkill;
                                        h.AssignedCharacterId = c.Id;
                                        break;
                                    }

                                    if (houseSkill.Level > bestHouseSkill.Level)
                                    {
                                        bestHouseSkill = houseSkill;
                                        h.AssignedCharacterId = c.Id;
                                    }
                                }

                                h.Bonus = CalculateHouseExpBonus(bestHouseSkill);
                            }
                        }

                        h.IsActive = isActive;
                        tHouses.Add(h);
                    }
                    town.TotalSlotCount = intSlotCount;
                    town.UsedSlotCount = tHouses.Count;
                    town.ActiveSlotCount = tHouses.Count(x => x.IsActive);
                    town.TownHouses = tHouses;
                    result.Add(town);
                }

                return result;
            });
        }

        /// <summary>
        ///     One town, described for the person who owns it.
        ///
        ///     Deliberately a second method rather than a change to <see cref="GetTownsAsync"/>,
        ///     which the public /towns page depends on. Three things differ, and all three are
        ///     what made that method unusable here: this looks the village up by user id so it
        ///     works off stream, it keeps every slot including the empty ones, and it carries the
        ///     slot number and the assigned character's name.
        /// </summary>
        public async Task<MyTownData> GetMyTownAsync(Guid userId)
        {
            return await Task.Run(() =>
            {
                var village = gameData.GetVillageByUserId(userId);
                if (village == null) return null;

                var owner = gameData.GetUser(village.UserId);
                var patreonTier = owner?.PatreonTier ?? 0;
                var resources = gameData.GetResources(village.ResourcesId);

                // Clamped the same way VillageManager.GetVillageInfo clamps it for the game client.
                // GameData.CreateVillage sets an administrator's Level from ExperienceForLevel(30)
                // rather than from 30, so those rows hold a level in the tens of thousands. The
                // slot count is capped anyway; this keeps the number on screen from being absurd.
                var level = Math.Min(village.Level, GameMath.MaxVillageLevel);

                var town = new MyTownData
                {
                    Id = village.Id,
                    Name = village.Name,
                    OwnerUserName = owner?.UserName,
                    Level = level,
                    Experience = village.Experience,
                    // Village experience counts progress within the current level: VillageProcessor
                    // subtracts the cost of a level when it awards one, the same as a clan does.
                    ExperienceForNextLevel = GameMath.ExperienceForLevel(level + 1),
                    TimeToNextLevel = EstimateTimeToNextLevel(level, village.Experience, patreonTier),
                    Coins = resources?.Coins ?? 0,
                    Wood = resources?.Wood ?? 0,
                    Ore = resources?.Ore ?? 0,
                    Fish = resources?.Fish ?? 0,
                    Wheat = resources?.Wheat ?? 0,
                    Magic = resources?.Magic ?? 0,
                    Arrows = resources?.Arrows ?? 0
                };

                // Get-or-create, the same call the game makes, so a town that has never been
                // opened in game still reports the slots its level has earned rather than none.
                var houses = gameData.GetOrCreateVillageHouses(village) ?? (IReadOnlyList<VillageHouse>)Array.Empty<VillageHouse>();
                town.Houses = houses.OrderBy(x => x.Slot).Select(x => DescribeHouse(x, village.UserId)).ToList();

                town.TotalSlotCount = town.Houses.Count;
                town.BuiltSlotCount = town.Houses.Count(x => x.IsBuilt);
                town.UsedSlotCount = town.Houses.Count(x => x.IsAssigned);
                town.ActiveSlotCount = town.Houses.Count(x => x.IsActive);

                // Slots come from town level, ten levels apiece, with Patreon tiers acting as a
                // floor. Working from the count granted rather than from the level is what makes
                // the answer right for a patron: their next slot from levelling is the one past
                // the floor they already have, not the next multiple of ten.
                town.SlotsFromLevel = Math.Min(level / 10, GameMath.MaxVillageLevel / 10);
                town.MaxSlotCount = GameMath.MaxVillageLevel / 10;
                town.NextSlotAtTownLevel = town.TotalSlotCount < town.MaxSlotCount
                    ? (town.TotalSlotCount + 1) * 10
                    : (int?)null;

                return town;
            });
        }

        /// <summary>
        ///     Resolves who is in a slot and what it is worth. Mirrors the public towns page: the
        ///     house records a user rather than a character, so an assignment is honoured through
        ///     whichever of that user's characters is locked to this stream, falling back to their
        ///     best one for the slot's skill when none of them is.
        /// </summary>
        private MyTownHouseData DescribeHouse(VillageHouse house, Guid ownerUserId)
        {
            var h = new MyTownHouseData
            {
                Id = house.Id,
                Slot = house.Slot,
                Type = (TownHouseSlotType)house.Type,
                AssignedUserId = house.UserId
            };

            // A slot with no house type on it gives nothing in game no matter who is assigned.
            // GetSkillByHouseType falls through to Mining for those values, so the bonus has to be
            // suppressed here rather than left to the shared helper to get right.
            if (!h.IsBuilt || house.UserId == null)
            {
                return h;
            }

            var assignedUser = gameData.GetUser(house.UserId.Value);
            h.AssignedUserName = assignedUser?.UserName;

            var candidates = new List<Character>();
            var named = house.CharacterId != null ? gameData.GetCharacter(house.CharacterId.Value) : null;
            if (named != null) candidates.Add(named);

            var others = gameData.GetCharactersByUserId(house.UserId.Value);
            if (others != null) candidates.AddRange(others.Where(x => x != null && x.Id != named?.Id));

            Character best = null;
            var bestSkill = new SkillStat();
            foreach (var c in candidates)
            {
                var skills = gameData.GetCharacterSkills(c.SkillsId);
                if (skills == null) continue;

                var skill = GetSkillByHouseType(skills, h.Type);
                if (c.UserIdLock == ownerUserId)
                {
                    best = c;
                    bestSkill = skill;
                    h.IsActive = true;
                    break;
                }

                if (best == null || skill.Level > bestSkill.Level)
                {
                    best = c;
                    bestSkill = skill;
                }
            }

            if (best == null) return h;

            h.AssignedCharacterId = best.Id;
            h.AssignedCharacterName = best.Name;
            h.SkillLevel = bestSkill.Level;
            h.Bonus = CalculateHouseExpBonus(bestSkill);
            return h;
        }

        /// <summary>
        ///     Roughly how much streaming the next town level takes.
        ///
        ///     VillageProcessor adds <see cref="GameMath.GetVillageExperience"/> once per session
        ///     tick with the elapsed time clamped to fifteen seconds, and that function is linear
        ///     in elapsed time, so asking it for one second gives the rate directly. Two things
        ///     about it are worth knowing and are not visible in game: the player count it passes
        ///     is a fixed 750 rather than the real one, so a town levels at the same speed however
        ///     many viewers are playing, and Mithril and above doubles the elapsed time it is
        ///     handed, which doubles the rate.
        /// </summary>
        private static TimeSpan? EstimateTimeToNextLevel(int level, double experience, int patreonTier)
        {
            if (level >= GameMath.MaxVillageLevel) return null;

            var perSecond = GameMath.GetVillageExperience(level, VillageProcessorPlayerCount, TimeSpan.FromSeconds(1));
            if (patreonTier >= (int)Patreon.Mithril) perSecond *= 2;
            if (perSecond <= 0) return null;

            var remaining = Math.Max(0, GameMath.ExperienceForLevel(level + 1) - experience);
            return TimeSpan.FromSeconds(remaining / perSecond);
        }

        public static SkillStat GetSkillByHouseType(Skills stats, TownHouseSlotType type)
        {
            switch (type)
            {
                case TownHouseSlotType.Woodcutting: return new SkillStat(stats.WoodcuttingLevel, stats.Woodcutting);
                case TownHouseSlotType.Mining: return new SkillStat(stats.MiningLevel, stats.Mining);
                case TownHouseSlotType.Farming: return new SkillStat(stats.FarmingLevel, stats.Farming);
                case TownHouseSlotType.Crafting: return new SkillStat(stats.CraftingLevel, stats.Crafting);
                case TownHouseSlotType.Cooking: return new SkillStat(stats.CookingLevel, stats.Cooking);
                case TownHouseSlotType.Slayer: return new SkillStat(stats.SlayerLevel, stats.Slayer);
                case TownHouseSlotType.Sailing: return new SkillStat(stats.SailingLevel, stats.Sailing);
                case TownHouseSlotType.Fishing: return new SkillStat(stats.FishingLevel, stats.Fishing);
                case TownHouseSlotType.Melee: return new SkillStat(stats.HealthLevel, stats.Health);
                case TownHouseSlotType.Healing: return new SkillStat(stats.HealingLevel, stats.Healing);
                case TownHouseSlotType.Magic: return new SkillStat(stats.MagicLevel, stats.Magic);
                case TownHouseSlotType.Ranged: return new SkillStat(stats.RangedLevel, stats.Ranged);
                case TownHouseSlotType.Gathering: return new SkillStat(stats.GatheringLevel, stats.Gathering);
                case TownHouseSlotType.Alchemy: return new SkillStat(stats.AlchemyLevel, stats.Alchemy);
                default: return new SkillStat(stats.MiningLevel, stats.Mining);
            }
        }
        public static float CalculateHouseExpBonus(SkillStat skill)
        {
            if (skill.Level == 0)
            {
                return 0;
            }

            // up to 200% exp bonus
            return (skill.Level / (float)GameMath.MaxLevel) * MaxExpBonusPerSlot;
        }
    }
    public struct SkillStat
    {

        public int Level;
        public double Experience;
        public SkillStat(int level, double experience)
        {
            this.Level = level;
            this.Experience = experience;
        }
    }

    public class TownData
    {
        public Guid Id { get; set; }
        public int Level { get; set; }
        public double Experience { get; set; }
        public string Name { get; set; }
        public TownOwnerData Owner { get; set; }
        public IReadOnlyList<TownHouseData> TownHouses { get; set; }
        public int TotalSlotCount { get; set; }
        public int UsedSlotCount { get; set; }
        public int ActiveSlotCount { get; set; }

        public float GetActiveBonus(TownHouseSlotType type)
        {
            if (TownHouses.Count == 0) return 0;
            var bonusValue = 0f;
            for (var i = 0; i < TownHouses.Count; i++)
            {
                var x = TownHouses[i];
                if (x.Type == type && x.IsActive) bonusValue += x.Bonus;
            }
            return bonusValue;
        }
    }

    public class TownHouseData
    {
        public Guid Id { get; set; }
        public Guid AssignedCharacterId { get; set; }
        public TownHouseSlotType Type { get; set; }
        public float Bonus { get; set; }
        public bool IsActive { get; set; }
    }

    public class TownOwnerData
    {
        public Guid Id { get; set; }
        public string UserName { get; set; }
    }

    /// <summary>
    ///     A town described for the person who owns it, which is a different question from the one
    ///     <see cref="TownData"/> answers for the public towns list. The difference that matters is
    ///     that every slot is here, including the empty ones: an empty slot is the thing the owner
    ///     can act on.
    /// </summary>
    public class MyTownData
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string OwnerUserName { get; set; }

        public int Level { get; set; }
        public double Experience { get; set; }
        public double ExperienceForNextLevel { get; set; }
        public TimeSpan? TimeToNextLevel { get; set; }

        public IReadOnlyList<MyTownHouseData> Houses { get; set; } = Array.Empty<MyTownHouseData>();

        public int TotalSlotCount { get; set; }
        public int BuiltSlotCount { get; set; }
        public int UsedSlotCount { get; set; }
        public int ActiveSlotCount { get; set; }

        public int SlotsFromLevel { get; set; }
        public int MaxSlotCount { get; set; }
        public int? NextSlotAtTownLevel { get; set; }

        public double Coins { get; set; }
        public double Wood { get; set; }
        public double Ore { get; set; }
        public double Fish { get; set; }
        public double Wheat { get; set; }
        public double Magic { get; set; }
        public double Arrows { get; set; }

        public double ExperienceToNextLevel => Math.Max(0, ExperienceForNextLevel - Experience);

        public double LevelProgress =>
            ExperienceForNextLevel <= 0 ? 0 : Math.Clamp(Experience / ExperienceForNextLevel, 0d, 1d);

        /// <summary>
        ///     Slots granted above what the town level has earned, which come from a Patreon tier.
        ///     Worth separating: levelling does not add a slot until the level catches the floor.
        /// </summary>
        public int SlotsFromPatreon => Math.Max(0, TotalSlotCount - SlotsFromLevel);

        /// <summary>Bonus actually being received, which is only the active slots.</summary>
        public float GetActiveBonus(TownHouseSlotType type)
        {
            var bonusValue = 0f;
            for (var i = 0; i < Houses.Count; i++)
            {
                var x = Houses[i];
                if (x.Type == type && x.IsActive) bonusValue += x.Bonus;
            }
            return bonusValue;
        }

        /// <summary>Bonus the slot would give if its assignee came back to the stream.</summary>
        public float GetIdleBonus(TownHouseSlotType type)
        {
            var bonusValue = 0f;
            for (var i = 0; i < Houses.Count; i++)
            {
                var x = Houses[i];
                if (x.Type == type && x.IsAssigned && !x.IsActive) bonusValue += x.Bonus;
            }
            return bonusValue;
        }
    }

    public class MyTownHouseData
    {
        public Guid Id { get; set; }
        public int Slot { get; set; }
        public TownHouseSlotType Type { get; set; }

        public Guid? AssignedUserId { get; set; }
        public string AssignedUserName { get; set; }
        public Guid AssignedCharacterId { get; set; }
        public string AssignedCharacterName { get; set; }

        /// <summary>The assignee's level in the skill this house type is built for.</summary>
        public int SkillLevel { get; set; }

        public float Bonus { get; set; }

        /// <summary>The assignee is playing on this stream, so the bonus is being received.</summary>
        public bool IsActive { get; set; }

        /// <summary>
        ///     A house type has been chosen for the slot. Undefined is a demolished slot and
        ///     NoSkill is one that has never been built, and neither gives a bonus to anything.
        /// </summary>
        public bool IsBuilt => Type > TownHouseSlotType.NoSkill;

        public bool IsAssigned => AssignedUserId != null && IsBuilt;
    }
}
