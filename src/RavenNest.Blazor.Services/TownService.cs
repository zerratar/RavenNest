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
}
