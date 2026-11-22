using RavenNest.BusinessLogic.Data;
using RavenNest.Models;
using System;
using System.Linq;
using RavenNest.BusinessLogic.Data;
using System.Collections.Concurrent;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public static class ItemDropRateSettings
    {
        public static int ResourceGatherInterval = 10;
        public static double DropChanceIncrement = 0.005;
        public static double InitDropChance = 0.5;

        /// <summary>Nest Finder (the Gathering skill tree): the chance of a pet egg in one gathering round, about one in 20 hours.</summary>
        public static double NestEggChance = RavenNest.Combat.TradeSkills.EggChancePerRound;
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

            // open to every level: the reward of a streamer's story (decided 2026-10-25), with an experience bonus in the game
            islandLevelRequirements[Island.Mangrove] = new ConcurrentDictionary<Skill, int>
            {
                [Skill.Woodcutting] = 1,
                [Skill.Gathering] = 1,
                [Skill.Fishing] = 1,
                [Skill.Farming] = 1,
                [Skill.Mining] = 1,
            };

            // the Ravebridge event island, open to every level; only the event takes anyone there
            islandLevelRequirements[Island.Ravebridge] = new ConcurrentDictionary<Skill, int>
            {
                [Skill.Woodcutting] = 1,
                [Skill.Gathering] = 1,
                [Skill.Fishing] = 1,
                [Skill.Farming] = 1,
                [Skill.Mining] = 1,
            };
        }

        /// <summary>
        /// Whether a stream may use an island. The mangrove island only where the streamer has finished its story
        /// (VillageStoryManager, island_mangrove); a modified game cannot gather there anywhere else.
        /// </summary>
        public static bool IslandOpen(GameData gameData, DataModels.GameSession session, Island island)
        {
            if (island != Island.Mangrove) return true;
            return session != null && gameData.GetStreamerUnlocks(session.UserId).Any(u => u.Key == "island_mangrove");
        }

        public bool TryGetIsland(string islandName, out Island island)
        {
            island = Island.None;
            if (string.IsNullOrEmpty(islandName))
            {
                return false;
            }
            Enum.TryParse<Island>(islandName, out island);
            if (island == Island.None || island == Island.Any || island == Island.Ferry)
            {
                return false;
            }
            return true;
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


        /// <summary>
        ///     One gathering round: the village's resource, the try at a find, and what the character's Gathering tree
        ///     adds (the game's docs/trade-skills-design.md, RavenNest.Combat.TradeSkills). Village's Share: sometimes
        ///     one more for the village. Tireless: sometimes the round counts twice (one more for the village and one
        ///     more try at a find; the finds' waits still count). Master of the Trade: two for the village a round in a
        ///     skill at 999. Sure Hands, Bountiful and Quick Return go to the find itself (SimpleDropHandler). Nest
        ///     Finder: a small chance of a gatherer pet's egg, once a round whatever Tireless did.
        ///     The quests' count of rounds (QuestTracker.Gathered) stays one a round: it counts the time gathered, not
        ///     what a node adds.
        /// </summary>
        /// <param name="ownLevel">The skill's own level: a node counts once the skill is at its level, gear aside.</param>
        /// <param name="level">The level finds are rolled with (gear counted).</param>
        /// <param name="addToVillage">Adds this many of the skill's resource to the village; null when the skill has none (Gathering).</param>
        /// <returns>Whether something was found.</returns>
        protected bool GatherRound(
            Microsoft.Extensions.Logging.ILogger logger,
            GameData gameData,
            PlayerInventory inventory,
            DataModels.GameSession session,
            DataModels.Character character,
            Skill skill,
            SimpleDropHandler drops,
            int ownLevel,
            int level,
            string taskArgument,
            Action<DataModels.Resources, int> addToVillage,
            Func<ResourceDrop, bool> canDrop = null)
        {
            var bonus = RavenNest.Combat.TradeSkills.Gathering(SkillTreeManager.BuildOf(gameData, character.Id), ownLevel);
            var rounds = bonus.DoubleRound > 0 && Random.NextDouble() < bonus.DoubleRound ? 2 : 1;
            var village = addToVillage != null ? GetVillageResources(gameData, session) : null;
            var found = false;
            for (var round = 0; round < rounds; ++round)
            {
                if (village != null)
                {
                    var amount = bonus.VillagePerRound;
                    if (bonus.VillageShare > 0 && Random.NextDouble() < bonus.VillageShare) ++amount;
                    addToVillage(village, amount);
                }

                if (round == 0) QuestTracker.Gathered(character, skill);
                found |= drops.TryDropItem(this, logger, gameData, inventory, session, character, level, taskArgument, canDrop, bonus);
            }

            if (bonus.Eggs && Random.NextDouble() < ItemDropRateSettings.NestEggChance)
                GiveNestEgg(gameData, inventory, session, character);

            return found;
        }

        /// <summary>
        ///     Nest Finder's egg: one from the gathering table (the eggs of pets that gather), into the inventory, and
        ///     the game is told so it can say it in chat (the play streak's event, with no days).
        /// </summary>
        private void GiveNestEgg(GameData gameData, PlayerInventory inventory, DataModels.GameSession session, DataModels.Character character)
        {
            var species = RavenNest.Combat.PetEggs.Pick(RavenNest.Combat.EggSource.Gathering, 0, Random.NextDouble());
            if (species == null) return;
            var itemId = RavenNest.Combat.PetEggs.ItemId(species.Id);
            if (gameData.GetItem(itemId) == null) return;
            IncrementItemStack(gameData, inventory, session, character, itemId);
            gameData.EnqueueGameEvent(gameData.CreateSessionEvent(GameEventType.PlayStreakEgg, session,
                new PlayStreakEgg { CharacterId = character.Id, Days = 0, Egg = species.EggName }));
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
        public Guid ItemId { get; }
        public string Name { get; set; }
        public double DropChance { get; set; }
        public double Cooldown { get; set; }
        public int SkillLevel { get; set; }
        public int? SkillIndex { get; set; }

        public ResourceDrop(Guid itemId, string name, double dropChance, double cooldown, int skillLevel, int? skillIndex)
        {
            ItemId = itemId;
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
