using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public class SimpleDropHandler
    {
        private static readonly Dictionary<string, DateTime> dropTimes;
        private static readonly Random dropRandom;

        static SimpleDropHandler()
        {
            dropRandom = new Random();
            dropTimes = new Dictionary<string, DateTime>();
            try
            {
                var droptimesJson = System.IO.Path.Combine(FolderPaths.GeneratedData, "resource-droptimes.json");
                if (System.IO.File.Exists(droptimesJson))
                {
                    dropTimes = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, DateTime>>(System.IO.File.ReadAllText(droptimesJson));
                }
            }
            catch { }
        }

        public static void SaveDropTimes()
        {
            try
            {
                var droptimesJson = System.IO.Path.Combine(FolderPaths.GeneratedData, "resource-droptimes.json");
                System.IO.File.WriteAllText(droptimesJson, Newtonsoft.Json.JsonConvert.SerializeObject(dropTimes));
            }
            catch { }
        }

        private readonly string skill;
        private readonly List<ResourceDrop> drops = new List<ResourceDrop>();

        private bool initialized;
        public SimpleDropHandler(string skill)
        {
            this.skill = skill;
        }

        public void ForceReloadDrops(GameData gameData)
        {
            this.drops.Clear();
            LoadDrops(gameData);
        }

        public void LoadDrops(GameData gameData)
        {
            var skillIndex = Skills.SkillNames.IndexOf(skill);
            foreach (var drop in gameData.GetResourceItemDrops().Where(x => x.Skill == null || x.Skill == skillIndex))
            {
                if (drop != null)
                {
                    drops.Add(drop);
                }
            }
        }

        public bool TryDropItem(
            ResourceTaskProcessor resProcessor,
            GameData gameData,
            PlayerInventoryProvider inventoryProvider,
            GameSession session,
            Character character,
            int skillLevel,
            string taskArgument,
            Func<ResourceDrop, bool> canDrop = null)
        {
            var chance = resProcessor.Random.NextDouble();
            if (chance > ItemDropRateSettings.InitDropChance)
            {
                return false;
            }

            LoadDropsIfRequired(gameData);

            if (drops.Count == 0)
            {
                return false;
            }

            var now = DateTime.UtcNow;

            var targetDrop = drops.FirstOrDefault(x => x.Name.ToLower() == taskArgument.ToLower());
            if (targetDrop != null)
            {
                if (TryDrop(gameData, inventoryProvider, session, resProcessor, character, skillLevel, targetDrop, canDrop))
                {
                    return true;
                }
            }

            foreach (var res in drops.OrderByRandomWeighted(x => x.SkillLevel, dropRandom))//drops.OrderByDescending(x => x.SkillLevel))
            {
                // we have already tested this one? if so skip it.
                if (targetDrop != null && res.Id == targetDrop.Id)
                {
                    continue;
                }

                if (TryDrop(gameData, inventoryProvider, session, resProcessor, character, skillLevel, res, canDrop))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryDrop(
            GameData gameData,
            PlayerInventoryProvider inventoryProvider,
            GameSession session,
            ResourceTaskProcessor resProcessor,
            Character character,
            int skillLevel, ResourceDrop targetDrop,
            Func<ResourceDrop, bool> canDrop = null)
        {
            var now = DateTime.UtcNow;

            var cooldownKey = character.Id + "_" + targetDrop.Id;
            if (targetDrop.Cooldown > 0 && dropTimes.TryGetValue(cooldownKey, out var lastDrop))
            {
                var timeSinceLastDrop = now - lastDrop;
                if (timeSinceLastDrop < TimeSpan.FromSeconds(targetDrop.Cooldown))
                {
                    return false;
                }
            }

            var chance = resProcessor.Random.NextDouble();
            var dropChance = targetDrop.GetDropChance(skillLevel);
            if (skillLevel >= targetDrop.SkillLevel && (chance <= dropChance))
            {
                if (canDrop == null || canDrop(targetDrop))
                {
                    dropTimes[cooldownKey] = now;
                    resProcessor.IncrementItemStack(gameData, inventoryProvider, session, character, targetDrop.Id);
                    return true;
                }
            }

            return false;
        }

        private void LoadDropsIfRequired(GameData gameData)
        {
            if (initialized || drops.Count > 0) return;
            LoadDrops(gameData);
            initialized = true;
        }
    }
}
