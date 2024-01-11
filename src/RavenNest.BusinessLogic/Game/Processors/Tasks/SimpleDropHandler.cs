using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using static RavenNest.BusinessLogic.GameMath;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public class SimpleDropHandler
    {
        private static readonly Random dropRandom;
        private static Dictionary<string, DateTime> dropTimes;

        private static readonly object mutex = new object();

        static SimpleDropHandler()
        {
            dropRandom = new Random();
            dropTimes = new Dictionary<string, DateTime>();
            try
            {
                lock (mutex)
                {
                    var droptimesJson = System.IO.Path.Combine(FolderPaths.GeneratedData, "resource-droptimes.json");
                    if (System.IO.File.Exists(droptimesJson))
                    {
                        dropTimes = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, DateTime>>(System.IO.File.ReadAllText(droptimesJson));
                    }
                }
            }
            catch { }

            if (dropTimes == null)
                dropTimes = new Dictionary<string, DateTime>();
        }

        public static void SaveDropTimes()
        {
            try
            {
                lock (mutex)
                {
                    var droptimesJson = System.IO.Path.Combine(FolderPaths.GeneratedData, "resource-droptimes.json");
                    System.IO.File.WriteAllText(droptimesJson, Newtonsoft.Json.JsonConvert.SerializeObject(dropTimes));
                }
            }
            catch (Exception exc)
            {
            }
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
            foreach (var drop in gameData.GetResourceItemDrops().Where(x => x != null && (x.Skill == null || x.Skill == skillIndex)))
            {
                if (drop != null)
                {
                    drops.Add(drop);
                }
            }
        }

        public bool TryDropItem(
            ResourceTaskProcessor resProcessor,
            ILogger logger,
            GameData gameData,
            PlayerInventoryProvider inventoryProvider,
            GameSession session,
            Character character,
            int skillLevel,
            string taskArgument,
            Func<ResourceDrop, bool> canDrop = null)
        {
            ResourceDrop drop = null;
            try
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

                // filter out bad drops
                var droppable = new List<ResourceDrop>();
                foreach (var d in drops)
                {
                    if (d == null || d.ItemId == Guid.Empty)
                    {
                        continue;
                    }

                    if (string.IsNullOrEmpty(d.Name))
                    {
                        var item = gameData.GetItem(d.ItemId);
                        if (item == null)
                        {
                            continue;
                        }

                        d.Name = item.Name;
                    }

                    droppable.Add(d);
                }

                var target = droppable.FirstOrDefault(x => x != null && x?.Name?.ToLower() == taskArgument?.ToLower());
                if (target != null)
                {
                    drop = target;
                    if (TryDrop(logger, gameData, inventoryProvider, session, resProcessor, character, skillLevel, target, canDrop))
                    {
                        return true;
                    }
                }


                foreach (var res in droppable.OrderByRandomWeighted(x => x.SkillLevel, dropRandom))//drops.OrderByDescending(x => x.SkillLevel))
                {
                    // we have already tested this one? if so skip it.
                    if (target != null && res.ItemId == target.ItemId)
                    {
                        continue;
                    }


                    drop = res;
                    if (TryDrop(logger, gameData, inventoryProvider, session, resProcessor, character, skillLevel, res, canDrop))
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (Exception exc)
            {
                var gameDataIsOK = gameData != null;
                var inventoryProviderIsOK = inventoryProvider != null;
                var sessionIsOK = session != null;
                var resProcessorIsOK = resProcessor != null;
                var characterIsOK = character != null;
                var dropIsOK = drop != null;
                var canDropIsOK = canDrop != null;
                logger.LogError($"Unable to drop item for player, GameData: {gameDataIsOK}, Inventory: {inventoryProviderIsOK}, Session: {sessionIsOK}, ResProcessor: {resProcessorIsOK}, Character: {characterIsOK}, Drop: {dropIsOK}, DropFunc: {canDropIsOK}, Exception: " + exc);
                return false;
            }
        }

        private bool TryDrop(
            ILogger logger,
            GameData gameData,
            PlayerInventoryProvider inventoryProvider,
            GameSession session,
            ResourceTaskProcessor resProcessor,
            Character character,
            int skillLevel, ResourceDrop targetDrop,
            Func<ResourceDrop, bool> canDrop = null)
        {
            var now = DateTime.UtcNow;

            var cooldownKey = GetCooldownKey(character, targetDrop);

            if (dropTimes == null)
            {
                dropTimes = new Dictionary<string, DateTime>();
            }

            if (targetDrop == null)
            {
                logger.LogError("Unable to drop items, target drop is null!");
                return false;
            }

            try
            {
                if (targetDrop.Cooldown > 0 && dropTimes.TryGetValue(cooldownKey, out var lastDrop))
                {
                    var timeSinceLastDrop = now - lastDrop;
                    if (timeSinceLastDrop < TimeSpan.FromSeconds(targetDrop.Cooldown))
                    {
                        return false;
                    }
                }
            }
            catch (Exception exc)
            {
                logger.LogError("Unable to drop items, unable to process drop cooldown! " + exc);
                return false;
            }

            if (resProcessor == null)
            {
                logger.LogError("Unable to drop items, res processor is null!");
                return false;
            }

            if (resProcessor.Random == null)
            {
                logger.LogError("Unable to drop items, res processor random is null!");
                return false;
            }

            try
            {
                var chance = resProcessor.Random.NextDouble();
                var dropChance = targetDrop.GetDropChance(skillLevel);
                if (skillLevel >= targetDrop.SkillLevel && (chance <= dropChance))
                {
                    if (canDrop == null || canDrop(targetDrop))
                    {
                        dropTimes[cooldownKey] = now;
                        resProcessor.IncrementItemStack(gameData, inventoryProvider, session, character, targetDrop.ItemId);
                        return true;
                    }
                }

                return false;
            }
            catch (Exception exc)
            {
                logger.LogError("Unable to drop items, exception: " + exc);
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string GetCooldownKey(Character c, ResourceDrop drop)
        {
            return c.Id + "_" + drop.ItemId;
        }

        private void LoadDropsIfRequired(GameData gameData)
        {
            if (initialized || drops.Count > 0) return;
            LoadDrops(gameData);
            initialized = true;
        }
    }
}
