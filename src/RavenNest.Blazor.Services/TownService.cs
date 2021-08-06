using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Net;
using RavenNest.DataModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TwitchLib.Api.Core.Extensions.System;

namespace RavenNest.Blazor.Services
{
    public class TownService
    {
        private readonly IGameData gameData;
        private const int OldMaxLevel = 170;
        private const float MaxExpBonusPerSlot = 50f;

        public TownService(IGameData gameData)
        {
            this.gameData = gameData;
        }

        public async Task<IReadOnlyList<TownData>> GetTownsAsync()
        {
            return await Task.Run(() =>
            {
                List<TownData> result = new List<TownData>();
                var activeSessions = this.gameData.GetActiveSessions();

                //var vilages = this.gameData.GetVillages();
                //foreach (var village in vilages)
                //{

                foreach (var sess in activeSessions)
                {
                    var village = gameData.GetVillageByUserId(sess.UserId);
                    if (village == null) continue;

                    var houses = this.gameData.GetVillageHouses(village);
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
                    town.Owner.UserId = owner?.UserId;
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
                        var chars = gameData.GetCharactersByUserId(house.UserId.GetValueOrDefault());
                        var isActive = false;
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
                default: return new SkillStat(stats.MiningLevel, stats.Mining);
            }
        }
        public static float CalculateHouseExpBonus(SkillStat skill)
        {
            if (skill.Level == 0)
            {
                return 0;
            }

            // up to 50% exp bonus
            return (skill.Level / (float)OldMaxLevel) * MaxExpBonusPerSlot;
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
        public long Experience { get; set; }
        public string Name { get; set; }
        public TownOwnerData Owner { get; set; }
        public IReadOnlyList<TownHouseData> TownHouses { get; set; }
        public int TotalSlotCount { get; set; }
        public int UsedSlotCount { get; set; }
        public int ActiveSlotCount { get; set; }

        public float GetActiveBonus(TownHouseSlotType type)
        {
            var h = TownHouses.Where(x => x.Type == type && x.IsActive).ToList();
            if (h.Count == 0)
            {
                return 0;
            }

            return h.Sum(x => x.Bonus);
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
        public string UserId { get; set; }
        public string UserName { get; set; }
    }
}
