using Shinobytes.Console.Forms;
using System.Runtime.CompilerServices;

using RavenNest.Tools.BackupLib;
using System.Linq;
using System;
using GameDataSimulation;
using RavenNest.DataModels;
using SkillType = GameDataSimulation.Skill;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace RavenNest.Tools.Actions
{
    public class PlayerSkillRollback
    {
        const double SlayerLevelMultiplier = 1.02;
        const double ExpMultiplier = 65;

        const int LevelDeltaMargin = 30;
        const int LevelDeltaMax = LevelDeltaMargin * 3;

        const int LevelSimilarityDeltaMargin = 5;
        const int LevelSimilarityDeltaMin = 2;


        public ProgressBar ToolProgress { get; }
        public TextBlock ToolStatus { get; }

        private readonly string rollbackDataFolder;
        private readonly string restorePointFolder;

        public PlayerSkillRollback(ProgressBar toolProgress, TextBlock toolStatus, string restorePointFolder, string rollbackDataFolder)
        {
            this.ToolProgress = toolProgress;
            this.ToolStatus = toolStatus;
            this.rollbackDataFolder = rollbackDataFolder;
            this.restorePointFolder = restorePointFolder;
        }

        public async void Apply()
        {
            var wasIndeterminate = this.ToolProgress.Indeterminate;

            var backupInfo = Backups.GetBackups(rollbackDataFolder);

            var maxSteps = backupInfo.Count + 14;
            var stepIndex = 0;

            this.ToolProgress.Indeterminate = false;
            this.ToolProgress.MaxValue = 100f;

            int IncrementProgress()
            {
                return (int)((stepIndex++ / maxSteps) * 100f);
            }

            /*
                Rules:
                * No players should be removed. 
                * Players newer than 2022-04-06 should not be updated
                * Only players where last active is after 2022-04-06
                * Any skill jumped quickly within a short time should be reverted back to time before it jumped.
                * Exp gained from time exp no longer jumps should be persisted.
                * CharacterSkillRecord can be truncated, but best would be to persist data for anyone not being updated.
                 
                Returns:
                    Result of the rollback should be in form of a restore point. but only the .json files affected. Otherwise the rollback will take a long time.
            */

            // most important files are character, skills and characterskillrecord. but only skills and characterskillrecord should be the output .json files. 

            await Task.Run(() =>
            {

                // get all sub directories in the rollbackdatafolder, they are sperated per date.
                this.ToolProgress.Value = IncrementProgress();
                this.ToolStatus.Text = "Loading Backup Files...";


                var backups = Backups.GetSkillBackups(rollbackDataFolder, (val, max) =>
                {
                    ToolProgress.Value = (int)((val / (float)max) * 100f);
                });

                this.ToolProgress.Value = IncrementProgress();
                this.ToolStatus.Text = "Saving Empty Character Skill Records";
                // 1. we know we will have to truncate the character skill record. so lets save an empty one right away.
                Write(new DataModels.CharacterSkillRecord[0]);


                var earliestFirst = backups.OrderBy(x => x.Created).ToList();

                // Earliest one should be our initial state and also our worst case rollback to. Which means this is our ground truth.
                // Last item should be our latest state and current one in game
                var groundTruth = earliestFirst[0];
                var actual = earliestFirst[^1];


                List<SkillChangeRecord> changes = new List<SkillChangeRecord>();
                List<SkillChangeMatch> ignored = new List<SkillChangeMatch>();
                List<SkillChangeMatch> forced = new List<SkillChangeMatch>();

                void Ignored(string query)
                {
                    if (!query.Contains(' '))
                    {
                        ignored.Add(new SkillChangeMatch
                        {
                            Name = query
                        });
                        return;
                    }

                    var d = query.Split(' ');
                    ignored.Add(new SkillChangeMatch
                    {
                        Name = d[0],
                        SkillName = d[1]
                    });
                }


                void Forced(string query)
                {
                    var d = query.Split(' ');
                    if (d.Length == 2)
                    {
                        forced.Add(new SkillChangeMatch
                        {
                            Name = d[0],
                            SkillName = d[1]
                        });
                    }
                    else if (d.Length > 2)
                    {
                        for (var i = 1; i < d.Length; ++i)
                        {
                            forced.Add(new SkillChangeMatch
                            {
                                Name = d[0],
                                SkillName = d[i]
                            });
                        }
                    }
                }

                this.ToolProgress.Value = IncrementProgress();
                this.ToolStatus.Text = "Configuring Rules...";
                /*
                    List exceptions taken from discord.
                 */
                Ignored("zerratar");
                Ignored("mhney fishing");
                Ignored("verynasty1#1 ranged");
                Ignored("blm_blacklivesmatterstill#0 magic");

                Ignored("hendi_cdn#1 healing");
                Ignored("ar0wann#2 healing");
                Ignored("toilet_peipa#2 healing");
                Ignored("hayhaythats#0 healing");
                Ignored("rexcower#1 healing");
                Ignored("damocles90#1 healing");

                Ignored("indiana_cojones#2");
                Ignored("seraphinne#0");

                /*
                    List of forced changed
                 */
                // verynasty1 had slayer skill transferred from magic
                Forced("zzjing#1 magic ranged");
                Forced("verynasty1#1 slayer");
                Forced("t3phie#0 ranged");

                // needs the slayer level rolled back.
                Forced("toilet_peipa#1 slayer");

                // lv99redfighter: I don't see my alt on the list, "lv99redfighter" and I believe it should be.  73 levels in magic and ranged, never trained either of those on that.
                Forced("lv99redfighter ranged");
                Forced("lv99redfighter magic");

                this.ToolProgress.Value = IncrementProgress();
                this.ToolStatus.Text = "Filtering Pass 0";

                // first of all, lets get separate affected and unaffected players
                // as we will join them in the end, we will use the actual data for the unaffected players.
                var workset = FilterByCreationAndLastUsed(actual, forced, ignored, groundTruth.Created);

                // with a workset, we can now go through all affected characters and do a second pass.
                // this time, a more thourough one. But to do that. we need to pull the affected one from each backup            

                this.ToolProgress.Value = IncrementProgress();
                this.ToolStatus.Text = "Filtering Pass 1";
                // But before that. Lets do one more pass on filtering by comparing groundTruth with actual affected one
                // this will allow us to roughly get out characters with skills that went crazy past those days.
                workset = FilterByImpossibleExpGain(workset, groundTruth, forced, ExpMultiplier);

                // most expensive step, we have to check all affected in workset
                // with previous backups, going backwards until we can find the deviation.
                // We don't need to test against first one as we already did that.


                this.ToolProgress.Value = IncrementProgress();
                this.ToolStatus.Text = "Creating Changeset for Rollback...";
                // we can also do the other way around, to find the last with deviation. if this isnt enough.
                for (var i = backups.Count - 2; i >= 0; --i)
                {
                    var backup = backups[i];

                    if (backup.Created == workset.Affected.Created) continue;
                    var tmpWorkset = FilterByImpossibleExpGain(workset, backup, forced, ExpMultiplier);

                    this.ToolProgress.Value = IncrementProgress();

                    if (tmpWorkset.Affected.Characters.Count > 0)
                    {
                        // we will re-assign our workset to a reverted skill state
                        // so we can later compare with a previous state before that and apply reverts
                        // until no longer affected.
                        workset = RevertSkillsTo(tmpWorkset, workset, backup, ExpMultiplier, forced, ignored, ref changes);
                        this.ToolProgress.Value = IncrementProgress();
                    }
                }

                this.ToolStatus.Text = "Merging Changeset Records...";
                // review changes, then save changes to disk.

                MergeRecords(ref changes);

                var playerChanges = changes.GroupBy(x => x.CharacterId).ToList();

                this.ToolStatus.Text = "Saving Changeset Records to changes.txt...";
                this.ToolProgress.Value = IncrementProgress();

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("A total of " + changes.Count + " changes will be made. " + playerChanges.Count + " players affected.");
                sb.AppendLine();
                foreach (var change in playerChanges)
                {
                    var items = change.ToList();

                    sb.AppendLine(items[0].CharacterName + "#" + items[0].CharacterIndex);

                    foreach (var c in items)
                    {
                        var reason = c.Reason;
                        if (string.IsNullOrEmpty(reason))
                        {
                            reason =
                                c.SimilarSkillLevel == c.LevelFrom
                                ? "Copy of " + c.SimilarSkillName + " " + c.SimilarSkillLevel
                                : "Most likely a copy of " + c.SimilarSkillName + " " + c.SimilarSkillLevel;

                            if (c.SimilarSkillLevel == 0)
                            {
                                reason = "Exp gained too quickly";
                            }
                        }

                        sb.AppendLine(c.SkillName.PadRight(10, ' ') + "\t" + c.LevelFrom.ToString().PadRight(3, ' ') + " => " + c.LevelTo.ToString().PadRight(3, ' ') + "\t(" + c.From + " => " + c.To + ") Reason: " + reason);
                    }
                    sb.AppendLine();
                }

                System.IO.File.WriteAllText(System.IO.Path.Combine(restorePointFolder, "changes.txt"), sb.ToString());


                this.ToolStatus.Text = "Creating Restorepoint...";
                this.ToolProgress.Value = IncrementProgress();
                // final step is as easy as combining unaffected and affected lists in the current workset and save the characterskills.json file.
                // and then verify that all players are still in there by reading the file one more time and double check.

                var restorePointSkillListWrite = new List<Skills>();
                restorePointSkillListWrite.AddRange(workset.Unaffected.Skills.Values);
                restorePointSkillListWrite.AddRange(workset.Affected.Skills.Values);
                Write(restorePointSkillListWrite);


                this.ToolStatus.Text = "Validating Restorepoint...";
                this.ToolProgress.Value = IncrementProgress();

                var restorePointSkillListRead = Read<Skills>().ToDictionary(x => x.Id, x => x);

                var missingSkillsCount = 0;
                foreach (var c in actual.Characters)
                {
                    if (!restorePointSkillListRead.TryGetValue(c.Value.SkillsId, out _))
                    {
                        missingSkillsCount++;
                    }
                }

                if (missingSkillsCount > 0)
                {
                    this.ToolStatus.Text = "Rollback Restorepoint Failed. " + missingSkillsCount + " Character Skills Records Missing!!";
                    this.ToolProgress.Value = IncrementProgress();
                }
                else
                {
                    this.ToolStatus.Text = "Rollback Restorepoint Completed.";
                    this.ToolProgress.Value = IncrementProgress();
                }
            });

            GC.Collect();

            this.ToolProgress.Indeterminate = wasIndeterminate;
        }

        private void MergeRecords(ref List<SkillChangeRecord> changes)
        {
            var records = new Dictionary<string, SkillChangeRecord>();

            foreach (var c in changes)
            {
                var key = c.CharacterId + "-" + c.SkillIndex;

                if (records.TryGetValue(key, out var change))
                {
                    change.EstimatedTrainTime += c.EstimatedTrainTime;

                    if (c.LevelTo < change.LevelTo)
                    {
                        change.LevelTo = c.LevelTo;
                        change.ExperienceTo = c.ExperienceTo;
                        change.To = c.To;
                    }
                    else
                    {
                        change.LevelFrom = c.LevelFrom;
                        change.ExperienceFrom = c.ExperienceFrom;
                        change.From = c.From;
                    }

                    // Could probably do some analysis to check which ones are the most affected though.

                    //var isWidelyAffectedSkill = (c.SimilarSkillName == "Ranged" || c.SimilarSkillName == "Magic" || c.SimilarSkillName == "Slayer");
                    if (Math.Abs(c.LevelTo - c.SimilarSkillLevel) < Math.Abs(change.LevelTo - change.SimilarSkillLevel) && c.SimilarSkillLevel > 0)
                    {
                        change.SimilarSkillName = c.SimilarSkillName;
                        change.SimilarSkillLevel = c.SimilarSkillLevel;
                    }
                    continue;
                }

                records[key] = c;
            }

            changes = records.Values.ToList();
        }

        private static void AddExperience(StatsUpdater skill, double exp)
        {
            var level = skill.Level;
            var experience = skill.Experience;

            experience += exp;
            var expForNextLevel = GameMath.ExperienceForLevel(skill.Level + 1);
            while (experience >= expForNextLevel)
            {
                ++level;
                experience -= expForNextLevel;

                expForNextLevel = GameMath.ExperienceForLevel(level + 1);
            }

            skill.Level = level;
            skill.Experience = experience;
        }

        private CharacterSkillBackupWorkSet RevertSkillsTo(
            CharacterSkillBackupWorkSet selection,
            CharacterSkillBackupWorkSet workset,
            CharacterSkillBackup backup,
            double multiplier,
            List<SkillChangeMatch> forced,
            List<SkillChangeMatch> exceptions,
            ref List<SkillChangeRecord> changes)
        {

            if (changes == null) changes = new List<SkillChangeRecord>();
            var dest = new CharacterSkillBackupWorkSet();
            dest.Affected = new CharacterSkillBackup(workset.Affected); // affected can never be less than the workset's affected.
            dest.Unaffected = new CharacterSkillBackup(workset.Unaffected); // unaffected should be kept unaffected.

            var elapsedTime = workset.Affected.Created - backup.Created;



            foreach (var characterId in selection.Affected.Characters.Keys)
            {
                var character = workset.Affected.Characters[characterId];


                var current = workset.Affected.Skills[character.SkillsId];
                var currentSkills = current.GetSkills();
                // if skills does not exist here, we have done something really bad in a previous filter, so let it throw.
                var old = backup.Skills[character.SkillsId];
                var oldSkills = old.GetSkills();

                var exc = exceptions.Where(x => x.Match(character)).ToList();
                var fm = forced.Where(x => x.Match(character)).ToList();
                // we can either choose to lerp actual time with possible gained exp. However, the problem does not seem to be affecting multiple
                // skills at the same time nor does it give you more exp than normal. It is that we have pretty much replaced one skill with another.
                var recorded = new HashSet<int>();
                foreach (var s0 in currentSkills)
                {
                    //if (recorded.Contains(s0.Index)) continue;
                    var olds0 = old.GetSkill(s0.Name);
                    var skillDelta = s0.Level - olds0.Level;

                    var changed = false;

                    // check if we match an excepted character.
                    // then just continue.
                    var wasForciblyIgnored = false;
                    foreach (var exception in exc)
                    {
                        if (exception != null && exception.Match(s0))
                        {
                            wasForciblyIgnored = true;
                            break;
                        }
                    }

                    if (wasForciblyIgnored)
                    {
                        continue;
                    }

                    // players affected by slayer level bug should be awarded additional slayer levels.
                    if ((SkillType)s0.Index == SkillType.Slayer)
                    {
                        //var oldLevel = olds0.Level;
                        var ticks = GameMath.Exp.GetTicksPerMinute(SkillType.Slayer) * (elapsedTime.TotalMinutes / 3);
                        var gainedExp = GetExperience(SkillType.Slayer, olds0.Level + 1, multiplier) * ticks; // not perfect, we could have adjusted it per level.
                        AddExperience(olds0, gainedExp);
                        //olds0.Level = (int)(olds0.Level * SlayerLevelMultiplier);
                    }

                    var wasForciblyAdded = false;
                    foreach (var forcedMatch in fm)
                    {
                        if (forcedMatch != null && forcedMatch.Match(s0))
                        {
                            wasForciblyAdded = true;
                            changes.Add(CreateRecord(selection, backup, characterId, character, s0, olds0, TimeSpan.Zero, forcedMatch.SkillCompareName, -1, "Requested by player"));
                            current.Set(s0.Index, olds0.Level, olds0.Experience);
                            recorded.Add(s0.Index);
                            break;
                        }
                    }

                    if (wasForciblyAdded)
                    {
                        continue;
                    }

                    foreach (var s1 in currentSkills)
                    {
                        //if (recorded.Contains(s1.Index)) continue;
                        if (s0.Name == s1.Name) { continue; }
                        var similarityDelta = Math.Abs(s0.Level - s1.Level);
                        if (similarityDelta < LevelSimilarityDeltaMargin)
                        {
                            // Inspect these two skills with backup skill.
                            // the skill that has gained impossible exp is the bad skill.
                            var olds1 = old.GetSkill(s1.Name);

                            var skill0Delta = s0.Level - olds0.Level;
                            if (skill0Delta >= LevelDeltaMargin)
                            {
                                var estimated0 = EstimateTrainingTime((SkillType)s0.Index, olds0.Level, s0.Level, multiplier);
                                if (estimated0 >= elapsedTime || (skill0Delta >= LevelDeltaMax && similarityDelta <= LevelSimilarityDeltaMin))
                                {
                                    changes.Add(CreateRecord(selection, backup, characterId, character, s0, olds0, estimated0, s1.Name, s1.Level));
                                    current.Set(s0.Index, olds0.Level, olds0.Experience);
                                    recorded.Add(s0.Index);
                                    changed = true;
                                }
                            }

                            var skill1Delta = s1.Level - olds1.Level;
                            if (skill1Delta >= LevelDeltaMargin)
                            {
                                var estimated1 = EstimateTrainingTime((SkillType)s1.Index, olds1.Level, s1.Level, multiplier);
                                if (estimated1 >= elapsedTime || (skill1Delta >= LevelDeltaMax && similarityDelta <= LevelSimilarityDeltaMin))
                                {
                                    changes.Add(CreateRecord(selection, backup, characterId, character, s1, olds1, estimated1, s0.Name, s0.Level));
                                    current.Set(s1.Index, olds1.Level, olds1.Experience);
                                    recorded.Add(s1.Index);
                                }
                            }
                        }
                    }

                    if (!changed)
                    {

                        //if (character.Name.Contains("damocl", StringComparison.OrdinalIgnoreCase))
                        //{
                        //    var s = (SkillType)s0.Index;
                        //    if (s == SkillType.Ranged)
                        //    {
                        //    }
                        //}

                        if (skillDelta >= LevelDeltaMax * 2)
                        {
                            var estimated0 = EstimateTrainingTime((SkillType)s0.Index, olds0.Level, s0.Level, multiplier);
                            //if (estimated0 >= elapsedTime)
                            //{
                            changes.Add(CreateRecord(selection, backup, characterId, character, s0, olds0, estimated0, "", 0));
                            current.Set(s0.Index, olds0.Level, olds0.Experience);
                            recorded.Add(s0.Index);
                            //}
                        }
                    }

                }
            }

            return dest;
        }

        private static SkillChangeRecord CreateRecord(CharacterSkillBackupWorkSet selection, CharacterSkillBackup backup, Guid characterId, Character character, StatsUpdater s0, StatsUpdater olds0, TimeSpan estimated0, string similarSkillName, int similarSkilllevel, string reason = null)
        {
            return new SkillChangeRecord
            {
                CharacterId = characterId,
                CharacterIndex = character.CharacterIndex,
                CharacterName = character.Name,
                EstimatedTrainTime = estimated0,
                SkillIndex = s0.Index,
                SkillName = s0.Name,
                ExperienceFrom = olds0.Experience,
                ExperienceTo = s0.Experience,
                LevelFrom = s0.Level,
                LevelTo = olds0.Level,
                From = selection.Affected.Created,
                To = backup.Created,
                SimilarSkillName = similarSkillName,
                SimilarSkillLevel = similarSkilllevel,
                Reason = reason
            };
        }

        /// <summary>
        ///     Second pass of the filtering. This is to compare with the ground truth and see which skills have been gained exp too fast
        /// </summary>
        /// <param name="src"></param>
        /// <param name="groundTruth"></param>
        /// <returns></returns>
        private static CharacterSkillBackupWorkSet FilterByImpossibleExpGain(CharacterSkillBackupWorkSet src, CharacterSkillBackup groundTruth, List<SkillChangeMatch> forced, double multiplier)
        {
            const int LevelDelta = 25;

            var dest = new CharacterSkillBackupWorkSet();
            dest.Affected = new CharacterSkillBackup(src.Affected.Created);
            dest.Unaffected = new CharacterSkillBackup(src.Unaffected); // we already know the unaffected are unaffected.

            var timeDelta = src.Affected.Created - groundTruth.Created;
            var timeMargin = 1.2d;

            foreach (var c in src.Affected.Characters)
            {
                var character = c.Value;
                var currentSkill = src.Affected.Skills[character.SkillsId];

                var wasForciblyAdded = false;
                foreach (var match in forced.Where(x => x.Match(character)))
                {
                    if (match != null)
                    {
                        foreach (var skill in currentSkill.GetSkills())
                        {
                            if (match.Match(skill))
                            {
                                wasForciblyAdded = true;
                                dest.Affected.Skills[character.SkillsId] = currentSkill;
                                dest.Affected.Characters[character.Id] = character;
                            }
                        }
                    }
                }

                if (wasForciblyAdded)
                {
                    continue;
                }


                if (!groundTruth.Skills.TryGetValue(character.SkillsId, out var previousSkill))
                {
                    dest.Unaffected.Skills[character.SkillsId] = currentSkill;
                    dest.Unaffected.Characters[character.Id] = character;
                    continue;
                }

                // if players jumped less than N levels, we will ignore it.
                var levelDelta = GetLevelDelta(currentSkill, previousSkill);
                if (levelDelta.Max < LevelDelta)
                {
                    dest.Unaffected.Skills[character.SkillsId] = currentSkill;
                    dest.Unaffected.Characters[character.Id] = character;
                    continue;
                }


                // Check the estimated time it should have taken, with a x150 multi to reach the time. (x150 is avg possible with rested time)
                // we will also add in a tiny margin of 20% to account for huts and patreon benefits.
                var trainingTime = EstimateTrainingTime(previousSkill, currentSkill, multiplier);
                if ((levelDelta.Max < (LevelDelta * 3) || levelDelta.Total < (LevelDelta * 4)) && trainingTime <= timeDelta * timeMargin)
                {
                    dest.Unaffected.Skills[character.SkillsId] = currentSkill;
                    dest.Unaffected.Characters[character.Id] = character;
                    continue;
                }

                //if (levelDelta.Max > 50)
                //{
                //}

                dest.Affected.Skills[character.SkillsId] = currentSkill;
                dest.Affected.Characters[character.Id] = character;
            }

            return dest;
        }

        private struct LevelDelta
        {
            public int Max;
            public int Total;
        }

        private static LevelDelta GetLevelDelta(DataModels.Skills current, DataModels.Skills previous)
        {
            var maxLevelDelta = 0;
            var total = 0;
            foreach (var s in current.GetSkills())
            {
                var prev = previous.GetSkill(s.Name);
                var delta = s.Level - prev.Level;
                maxLevelDelta = Math.Max(maxLevelDelta, delta);
                total += delta;
            }

            return new LevelDelta
            {
                Max = maxLevelDelta,
                Total = total
            };
        }

        public static double GetExperience(SkillType skill, int nextLevel, double multiplier)
        {
            return GameMath.Exp.CalculateExperience(nextLevel, skill, 1.25d, multiplier, skill == SkillType.Slayer ? 0.1d : 1d);
        }

        private static TimeSpan EstimateTrainingTimeByExpGain(SkillType skill, int fromLevel, int toLevel, double multiplier)
        {
            var result = TimeSpan.Zero;
            var levelDelta = toLevel - fromLevel;
            var startLevel = fromLevel;
            for (var i = 0; i < levelDelta; ++i)
            {
                var nextLevel = startLevel + 1;
                var ticksPerSeconds = GameMath.Exp.GetTicksPerSeconds(skill);
                //var bTicksForLevel = GameMath.Exp.GetTotalTicksForLevel(nextLevel, skill, multiplier);

                var expPerTick = GetExperience(skill, nextLevel, multiplier: multiplier);
                var expRequiredForLevel = GameMath.ExperienceForLevel(nextLevel);
                var totalTicks = expRequiredForLevel / expPerTick;
                result += TimeSpan.FromSeconds(totalTicks / ticksPerSeconds);
            }
            return result;
        }

        /// <summary>
        /// Get the estimated time it should have taken to train all the skills from A to B, it will also take into consideration as if there was a x100 multiplier through the whole time. This is to ensure we have enough margins.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <returns></returns>
        private static TimeSpan EstimateTrainingTime(SkillType skill, int fromLevel, int toLevel, double multiplier)
        {
            return EstimateTrainingTimeByExpGain(skill, fromLevel, toLevel, multiplier);

            // Estimate based on Ticks, but not as accurate

            //var result = TimeSpan.Zero;
            //var levelDelta = toLevel - fromLevel;
            //var startLevel = fromLevel;
            //for (var i = 0; i < levelDelta; ++i)
            //{
            //    var nextLevel = startLevel + 1;
            //    var ticksPerSeconds = GameMath.Exp.GetTicksPerSeconds(skill);
            //    var bTicksForLevel = GameMath.Exp.GetTotalTicksForLevel(nextLevel, skill, multiplier);
            //    result += TimeSpan.FromSeconds(bTicksForLevel / ticksPerSeconds);
            //}
            //return result;
        }
        private static TimeSpan EstimateTrainingTime(DataModels.Skills from, DataModels.Skills to, double multiplier)
        {
            //var oldSkills = from.GetSkills();
            var newSkills = to.GetSkills();
            var result = TimeSpan.Zero;

            foreach (var ns in newSkills)
            {
                // For simplicity, we will ignore !train all and only take individual skills
                var skill = (SkillType)ns.Index;
                var os = from.GetSkill(ns.Name);
                result += EstimateTrainingTime(skill, os.Level, ns.Level, multiplier);
                //var levelDelta = ns.Level - os.Level;
                //var startLevel = os.Level;
                //for (var i = 0; i < levelDelta; ++i)
                //{
                //    var nextLevel = startLevel + 1;
                //    var ticksPerSeconds = GameMath.Exp.GetTicksPerSeconds(skill);
                //    var bTicksForLevel = GameMath.Exp.GetTotalTicksForLevel(nextLevel, skill, multiplier);
                //    result += TimeSpan.FromSeconds(bTicksForLevel / ticksPerSeconds);
                //}
            }

            return result;
        }


        /// <summary>
        /// The first pass of the filtering. This is used on the latest dataset.
        /// And is very rough, will only filter out characters that has not been used lately or was created after the bug.
        /// </summary>
        /// <param name="src"></param>
        /// <param name="minDate"></param>
        /// <returns></returns>
        private static CharacterSkillBackupWorkSet FilterByCreationAndLastUsed(CharacterSkillBackup src, List<SkillChangeMatch> forced, List<SkillChangeMatch> ignored, DateTime minDate)
        {
            var dest = new CharacterSkillBackupWorkSet();
            dest.Affected = new CharacterSkillBackup(src.Created);
            dest.Unaffected = new CharacterSkillBackup(src.Created);

            void Unaffected(DataModels.Character c) => PlayerSkillRollback.Unaffected(src, dest, c);
            void Affected(DataModels.Character c) => PlayerSkillRollback.Affected(src, dest, c);

            foreach (var c in src.Characters)
            {
                var character = c.Value;

                var match = forced.FirstOrDefault(x => x.Match(character));
                if (match != null)
                {
                    Affected(character);
                    continue;
                }

                var wasForciblyIgnored = false;
                foreach (var ignoredMatch in ignored.Where(x => x.Match(character)))
                {
                    if (ignoredMatch != null && ignoredMatch.SkillIndex == null && string.IsNullOrEmpty(ignoredMatch.SkillName))
                    {
                        wasForciblyIgnored = true;
                        Unaffected(character);
                        continue;
                    }
                }

                if (wasForciblyIgnored)
                {
                    continue;
                }

                // if we were created after the min date, we are unaffected.
                if (character.Created >= minDate)
                {
                    Unaffected(character);
                    continue;
                }

                if (character.LastUsed == null)
                {
                    Unaffected(character);
                    continue;
                }

                // this is a special case, if we last used was before the date
                // then we can assume they joined a game that was in a session pre v0.7.8.9 update (aka 0.7.8.8a)
                // this can only be assumed as players last used is updated even on restore, reloads, etc.
                if (character.LastUsed <= minDate)
                {
                    Unaffected(character);
                    continue;
                }

                Affected(character);
            }

            return dest;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Unaffected(CharacterSkillBackup source, CharacterSkillBackupWorkSet result, DataModels.Character s)
        {
            result.Unaffected.Characters[s.Id] = s;
            result.Unaffected.Skills[s.SkillsId] = source.Skills[s.SkillsId];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Affected(CharacterSkillBackup source, CharacterSkillBackupWorkSet result, DataModels.Character s, DataModels.Skills cSkills)
        {
            result.Affected.Characters[s.Id] = s;
            result.Affected.Skills[cSkills.Id] = cSkills;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Affected(CharacterSkillBackup source, CharacterSkillBackupWorkSet result, DataModels.Character s)
        {
            result.Affected.Characters[s.Id] = s;
            result.Affected.Skills[s.SkillsId] = source.Skills[s.SkillsId];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Write<T>(T data)
        {
            RestorepointUtilities.Write(restorePointFolder, data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private List<T> Read<T>()
        {
            return RestorepointUtilities.Read<T>(restorePointFolder);
        }
    }

    public class CharacterSkillBackupWorkSet
    {
        public CharacterSkillBackup Affected { get; set; }
        public CharacterSkillBackup Unaffected { get; set; }
    }

    public class SkillChangeMatch
    {
        private readonly Func<SkillChangeMatch, Character, bool> characterMatch;
        private readonly Func<SkillChangeMatch, StatsUpdater, bool> skillMatch;

        public string Name { get; set; }
        public int? Index { get; set; }
        public Guid? CharacterId { get; set; }
        public int? SkillIndex { get; set; }
        public string SkillName { get; set; }
        public string SkillCompareName { get; set; }
        public SkillChangeMatch()
        {
        }

        public SkillChangeMatch(Func<SkillChangeMatch, Character, bool> match)
        {
            this.characterMatch = match;
        }

        public SkillChangeMatch(Func<SkillChangeMatch, StatsUpdater, bool> skillMatch)
        {
            this.skillMatch = skillMatch;
        }

        public SkillChangeMatch(Func<SkillChangeMatch, Character, bool> characterMatch, Func<SkillChangeMatch, StatsUpdater, bool> skillMatch)
        {
            this.characterMatch = characterMatch;
            this.skillMatch = skillMatch;
        }

        public bool Match(StatsUpdater skill)
        {
            if (skillMatch != null)
            {
                return skillMatch(this, skill);
            }

            return skill.Index == SkillIndex || skill.Name.ToLower() == SkillName?.ToLower();
        }

        public bool Match(Character c)
        {
            if (characterMatch != null)
            {
                return characterMatch(this, c);
            }

            if (c == null) return false;
            if (CharacterId != null && CharacterId.Value == c.Id)
                return true;

            if (!string.IsNullOrEmpty(Name))
            {
                if (Name.Contains("#") && Index == null)
                {
                    var d = Name.Split('#');
                    Index = int.Parse(d[1]);
                    Name = d[0];
                }

                if (Index != null)
                {
                    return Index == c.CharacterIndex && Name.ToLower() == c.Name.ToLower();
                }

                return Name.ToLower() == c.Name.ToLower();
            }

            return false;
        }
    }

    public class SkillChangeRecord
    {
        public Guid CharacterId { get; set; }
        public int CharacterIndex { get; set; }
        public int LevelFrom { get; set; }
        public int LevelTo { get; set; }
        public double ExperienceFrom { get; set; }
        public double ExperienceTo { get; set; }
        public int SkillIndex { get; set; }
        public TimeSpan EstimatedTrainTime { get; set; }

        // For inspection
        public string CharacterName { get; set; }

        // For inspection
        public string SkillName { get; set; }
        public string SimilarSkillName { get; set; }
        public int SimilarSkillLevel { get; set; }
        public string Reason { get; set; }
        public DateTime From { get; set; }
        public DateTime To { get; set; }
    }
}
