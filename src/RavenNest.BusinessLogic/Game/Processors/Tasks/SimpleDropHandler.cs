using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Providers;
using RavenNest.DataModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public class SimpleDropHandler
    {
        private readonly string skill;
        private readonly List<ResourceDrop> drops = new List<ResourceDrop>();

        private bool initialized;
        public SimpleDropHandler(string skill)
        {
            this.skill = skill;
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

        public bool TryDropItem(ResourceTaskProcessor resProcessor, GameData gameData, PlayerInventoryProvider inventoryProvider, GameSession session, Character character, int skillLevel, Func<ResourceDrop, bool> canDrop = null)
        {
            var chance = resProcessor.Random.NextDouble();
            if (chance > ItemDropRateSettings.InitDropChance)
            {
                return false;
            }

            LoadDropsIfRequired(gameData);

            foreach (var res in drops.OrderByDescending(x => x.SkillLevel))
            {
                chance = resProcessor.Random.NextDouble();
                if (skillLevel >= res.SkillLevel && (chance <= res.GetDropChance(skillLevel)))
                {
                    if (canDrop == null || canDrop(res))
                    {
                        resProcessor.IncrementItemStack(gameData, inventoryProvider, session, character, res.Id);
                        return true;
                    }
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
