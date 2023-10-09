using RavenNest.BusinessLogic.Data;
using RavenNest.Models;
using System;
using System.Collections.Concurrent;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public static class ItemDropRateSettings
    {
        public static int ResourceGatherInterval = 10;
        public static double DropChanceIncrement = 0.00025;
        public static double InitDropChance = 0.5;
    }

    public abstract class ResourceTaskProcessor : PlayerTaskProcessor
    {
        static ResourceTaskProcessor()
        {
        }

        public readonly ConcurrentDictionary<Island, ConcurrentDictionary<Skill, int>> islandLevelRequirements = new();
        public ResourceTaskProcessor()
        {
            islandLevelRequirements[Island.Home] = new ConcurrentDictionary<Skill, int>
            {
                [Skill.Woodcutting] = 1,
                [Skill.Gathering] = 1,
                [Skill.Fishing] = 1,
                [Skill.Farming] = 1,
                [Skill.Mining] = 1,
            };

            islandLevelRequirements[Island.Away] = new ConcurrentDictionary<Skill, int>
            {
                [Skill.Woodcutting] = 50,
                [Skill.Gathering] = 50,
                [Skill.Fishing] = 50,
                [Skill.Farming] = 50,
                [Skill.Mining] = 50,
            };

            islandLevelRequirements[Island.Ironhill] = new ConcurrentDictionary<Skill, int>
            {
                [Skill.Woodcutting] = 100,
                [Skill.Gathering] = 100,
                [Skill.Fishing] = 100,
                [Skill.Farming] = 100,
                [Skill.Mining] = 100,
            };

            islandLevelRequirements[Island.Kyo] = new ConcurrentDictionary<Skill, int>
            {
                [Skill.Woodcutting] = 200,
                [Skill.Gathering] = 200,
                [Skill.Fishing] = 200,
                [Skill.Farming] = 200,
                [Skill.Mining] = 200,
            };

            islandLevelRequirements[Island.Heim] = new ConcurrentDictionary<Skill, int>
            {
                [Skill.Woodcutting] = 300,
                [Skill.Gathering] = 300,
                [Skill.Fishing] = 300,
                [Skill.Farming] = 300,
                [Skill.Mining] = 300,
            };

            islandLevelRequirements[Island.Atria] = new ConcurrentDictionary<Skill, int>
            {
                [Skill.Woodcutting] = 500,
                [Skill.Gathering] = 500,
                [Skill.Fishing] = 500,
                [Skill.Farming] = 500,
                [Skill.Mining] = 500,
            };

            islandLevelRequirements[Island.Eldara] = new ConcurrentDictionary<Skill, int>
            {
                [Skill.Woodcutting] = 700,
                [Skill.Gathering] = 700,
                [Skill.Fishing] = 700,
                [Skill.Farming] = 700,
                [Skill.Mining] = 700,
            };
        }

        public bool TryGetIsland(string islandName, out Island island)
        {
            island = Island.None;
            if (string.IsNullOrEmpty(islandName))
            {
                return false;
            }

            return Enum.TryParse<Island>(islandName, out island);
        }

        protected void UpdateResourceGain(
            GameData gameData,
            DataModels.GameSession session,
            DataModels.Character character,
            Action<DataModels.Resources> onUpdate)
        {
            var now = DateTime.UtcNow;
            var state = gameData.GetCharacterSessionState(session.Id, character.Id);
            if (now - state.LastTaskUpdate >= TimeSpan.FromSeconds(ItemDropRateSettings.ResourceGatherInterval))
            {
                session.Updated = DateTime.UtcNow;

                var resources = gameData.GetResources(character);

                state.LastTaskUpdate = DateTime.UtcNow;
                state.SailingRewardAttempted = DateTime.UnixEpoch;

                onUpdate?.Invoke(resources);
            }
        }

        protected DataModels.Resources GetVillageResources(GameData gameData, DataModels.GameSession session)
        {
            DataModels.Resources resx = null;
            var village = gameData.GetVillageBySession(session);
            if (village != null)
            {
                resx = gameData.GetResources(village.ResourcesId);
            }
            return resx;
        }
    }

    public class ResourceDrop
    {
        public Guid Id { get; }
        public string Name { get; }
        public double DropChance { get; }
        public double Cooldown { get; }
        public int SkillLevel { get; set; }
        public int? SkillIndex { get; set; }

        public ResourceDrop(Guid id, string name, double dropChance, double cooldown, int skillLevel, int? skillIndex)
        {
            Id = id;
            Name = name;
            DropChance = dropChance;
            Cooldown = cooldown;
            SkillLevel = skillLevel;
            SkillIndex = skillIndex;
        }

        public double GetDropChance(int playerSkillLevel)
        {
            return (DropChance + ((playerSkillLevel - SkillLevel) * ItemDropRateSettings.DropChanceIncrement));
        }

        public static implicit operator ResourceDrop(DataModels.ResourceItemDrop source)
        {
            return new ResourceDrop(source.ItemId, source.ItemName, source.DropChance, source.Cooldown ?? 0, source.LevelRequirement, source.Skill);
        }
    }
}
