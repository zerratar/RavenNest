using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Timers;
using TwitchLib.Api.Auth;
using static RavenNest.BusinessLogic.GameMath;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public class SimpleDropHandler
    {
        private static readonly Random dropRandom;
        private static ConcurrentDictionary<string, DateTime> dropTimes;
        private static Timer saveTimer;
        private static readonly object dropsMutex = new object();
        private static readonly object fileMutex = new object();

        public static bool SaveEnabled = true;

        public static void StopSaving()
        {
            SaveEnabled = false;
            try
            {
                saveTimer.Stop();
                saveTimer.Dispose();
            }
            catch { }
        }

        static SimpleDropHandler()
        {
            dropRandom = new Random();
            dropTimes = new ConcurrentDictionary<string, DateTime>();

            saveTimer = new System.Timers.Timer(5000);
            saveTimer.Elapsed += (s, e) =>
            {
                if (SaveEnabled)
                    SaveDropTimes();

                if (!SaveEnabled)
                {
                    try
                    {
                        saveTimer.Stop();
                        saveTimer.Dispose();
                    }
                    catch { }
                }
            };

            try
            {
                lock (fileMutex)
                {
                    var droptimesJson = System.IO.Path.Combine(FolderPaths.GeneratedDataPath, "resource-droptimes.json");
                    if (System.IO.File.Exists(droptimesJson))
                    {
                        dropTimes = Newtonsoft.Json.JsonConvert.DeserializeObject<ConcurrentDictionary<string, DateTime>>(System.IO.File.ReadAllText(droptimesJson));
                    }
                }
            }
            catch { }

            if (dropTimes == null)
                dropTimes = new ConcurrentDictionary<string, DateTime>();
        }

        public static void SaveDropTimes()
        {
            try
            {
                lock (fileMutex)
                {
                    var droptimesJson = System.IO.Path.Combine(FolderPaths.GeneratedDataPath, "resource-droptimes.json");
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
            lock (dropsMutex)
            {
                this.drops.Clear();
            }

            LoadDrops(gameData);
        }

        public void LoadDrops(GameData gameData)
        {

            var skillIndex = Skills.SkillNames.IndexOf(skill);
            var dropsToAdd = new List<ResourceDrop>();
            foreach (var drop in gameData.GetResourceItemDrops().Where(x => x != null && (x.Skill == null || x.Skill == skillIndex)))
            {
                if (drop != null)
                {
                    dropsToAdd.Add(drop);

                }
            }
            lock (dropsMutex)
            {
                if (dropsToAdd.Count > 0)
                    drops.AddRange(dropsToAdd);
            }
        }

        public bool TryDropItem(
            ResourceTaskProcessor resProcessor,
            ILogger logger,
            GameData gameData,
            PlayerInventory inventory,
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
                List<ResourceDrop> dr;

                lock (dropsMutex)
                {
                    dr = drops.ToList();
                }

                if (dr.Count == 0)
                {
                    return false;
                }

                var now = DateTime.UtcNow;

                // filter out bad drops
                var droppable = new List<ResourceDrop>();
                foreach (var d in dr)
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

                var target = droppable.FirstOrDefault(x => x != null && x?.Name?.ToLower().Trim() == taskArgument?.ToLower().Trim());
                if (target != null)
                {
                    drop = target;
                    if (TryDrop(logger, gameData, inventory, session, resProcessor, character, skillLevel, target, canDrop))
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
                    if (TryDrop(logger, gameData, inventory, session, resProcessor, character, skillLevel, res, canDrop))
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (Exception exc)
            {
                var gameDataIsOK = gameData != null;
                var inventoryIsOk = inventory != null;
                var sessionIsOK = session != null;
                var resProcessorIsOK = resProcessor != null;
                var characterIsOK = character != null;
                var dropIsOK = drop != null;
                var canDropIsOK = canDrop != null;
                logger.LogError($"Unable to drop item for player, GameData: {gameDataIsOK}, Inventory: {inventoryIsOk}, Session: {sessionIsOK}, ResProcessor: {resProcessorIsOK}, Character: {characterIsOK}, Drop: {dropIsOK}, DropFunc: {canDropIsOK}, Exception: " + exc);
                return false;
            }
        }

        private bool TryDrop(
            ILogger logger,
            GameData gameData,
            PlayerInventory inventory,
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
                dropTimes = new ConcurrentDictionary<string, DateTime>();
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
                        resProcessor.IncrementItemStack(gameData, inventory, session, character, targetDrop.ItemId);
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
            lock (dropsMutex)
            {
                if (initialized || drops.Count > 0) return;
            }
            LoadDrops(gameData);
            initialized = true;
        }
    }
}
