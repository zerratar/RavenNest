using System;
using System.Linq;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Providers;
using RavenNest.BusinessLogic.Extensions;
using RavenNest.Combat;
using RavenNest.DataModels;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    /// <summary>
    ///     The skill trees on the server (combat v2, the game's docs/combat-skills-design.md): learning nodes with points
    ///     from combat levels, the loadout, and respecs (coins, a free one, or a Tome of Forgetting). The rules live in
    ///     RavenNest.Combat.SkillBuild, shared with the game and the website; the server is the one that saves.
    /// </summary>
    public class SkillTreeManager
    {
        public const string TomeOfForgetting = "Tome of Forgetting";

        private readonly GameData gameData;
        private readonly PlayerInventoryProvider inventoryProvider;
        private readonly object mutex = new object();

        public SkillTreeManager(GameData gameData, PlayerInventoryProvider inventoryProvider)
        {
            this.gameData = gameData;
            this.inventoryProvider = inventoryProvider;
        }

        /// <summary>The eight combat skills added up, for the points.</summary>
        public static int TotalCombatLevels(DataModels.Skills s) =>
            s == null ? 0 : s.AttackLevel + s.DefenseLevel + s.StrengthLevel + s.HealthLevel + s.RangedLevel + s.MagicLevel + s.HealingLevel + s.SlayerLevel;

        public static Func<CombatSkill, int> LevelOf(DataModels.Skills s) => skill =>
        {
            if (s == null) return 1;
            switch (skill)
            {
                case CombatSkill.Attack: return s.AttackLevel;
                case CombatSkill.Defense: return s.DefenseLevel;
                case CombatSkill.Strength: return s.StrengthLevel;
                case CombatSkill.Health: return s.HealthLevel;
                case CombatSkill.Ranged: return s.RangedLevel;
                case CombatSkill.Magic: return s.MagicLevel;
                case CombatSkill.Healing: return s.HealingLevel;
                default: return s.SlayerLevel;
            }
        };

        /// <summary>The character's combat levels, for which nodes are open (the website's skill tree page).</summary>
        public Func<CombatSkill, int> LevelsOf(Guid characterId)
        {
            var character = gameData.GetCharacter(characterId);
            return LevelOf(character != null ? gameData.GetCharacterSkills(character.SkillsId) : null);
        }

        /// <summary>Whether the user may change this character's trees (their own character).</summary>
        public bool Owns(Guid userId, Guid characterId)
        {
            var character = gameData.GetCharacter(characterId);
            return character != null && character.UserId == userId;
        }

        public SkillBuildInfo Get(Guid characterId)
        {
            var character = gameData.GetCharacter(characterId);
            if (character == null) return null;
            var row = gameData.GetCharacterSkillBuild(characterId);
            if (row != null) lock (mutex) Upgrade(row);
            return Info(character, SkillBuild.Parse(row?.Build), row);
        }

        /// <summary>A build from before the trees' last balance update: a free respec, and the version noted.</summary>
        private static void Upgrade(CharacterSkillBuild row)
        {
            if (row.TreeVersion >= SkillTrees.Version) return;
            if (SkillBuild.Parse(row.Build).Spent > 0) row.FreeRespecs++;
            row.TreeVersion = SkillTrees.Version;
        }

        /// <summary>Puts when an active is used (the website); null rule text puts back the skill's default.</summary>
        public SkillBuildResult SetRule(Guid characterId, string nodeId, SkillUse rule)
        {
            return Change(characterId, (character, skills, build, row) => build.SetRule(nodeId, rule));
        }

        public SkillBuildResult Raise(Guid characterId, string nodeText)
        {
            return Change(characterId, (character, skills, build, row) =>
            {
                var node = SkillTrees.Find(nodeText);
                if (node == null) return "There is no skill called " + nodeText + ".";
                var reason = build.Raise(node.Id, LevelOf(skills), SkillTrees.EarnedPoints(TotalCombatLevels(skills)));
                if (reason != null) return reason;
                // a new active goes into the first free slot, so it is used at once
                if (node.IsActive && build.Rank(node.Id) == 1 && !build.Loadout.Contains(node.Id, StringComparer.OrdinalIgnoreCase))
                {
                    for (var slot = 0; slot < build.LoadoutSlots; ++slot)
                    {
                        if (slot >= build.Loadout.Count || string.IsNullOrEmpty(build.Loadout[slot]))
                        {
                            build.SetLoadout(slot, node.Id);
                            break;
                        }
                    }
                }
                return null;
            });
        }

        /// <summary>Puts an active in a slot (1 based), or empties it when <paramref name="nodeText"/> is empty.</summary>
        public SkillBuildResult SetLoadout(Guid characterId, int slot, string nodeText)
        {
            return Change(characterId, (character, skills, build, row) =>
            {
                if (string.IsNullOrWhiteSpace(nodeText) || nodeText == "-")
                {
                    build.ClearSlot(slot - 1);
                    return null;
                }
                var node = SkillTrees.Find(nodeText);
                if (node == null) return "There is no skill called " + nodeText + ".";
                return build.SetLoadout(slot - 1, node.Id);
            });
        }

        /// <summary>Every point back: free when a free respec is left, then a Tome of Forgetting, otherwise coins.</summary>
        public SkillBuildResult Respec(Guid characterId)
        {
            return Change(characterId, (character, skills, build, row) =>
            {
                var spent = build.Spent;
                if (spent == 0) return "There is nothing to give back.";

                if (row.FreeRespecs > 0)
                {
                    row.FreeRespecs--;
                }
                else if (TakeTome(character))
                {
                }
                else
                {
                    var cost = SkillTrees.RespecCost(spent);
                    var resources = gameData.GetResources(character);
                    if (resources == null || resources.Coins < cost) return "A respec costs " + cost.ToString("N0") + " coins.";
                    resources.Coins -= cost;
                }

                build.Reset();
                return null;
            });
        }

        private SkillBuildResult Change(Guid characterId, Func<Character, DataModels.Skills, SkillBuild, CharacterSkillBuild, string> change)
        {
            lock (mutex)
            {
                var character = gameData.GetCharacter(characterId);
                if (character == null) return new SkillBuildResult { Message = "No such character." };
                var skills = gameData.GetCharacterSkills(character.SkillsId);
                var row = GetOrCreate(characterId);
                Upgrade(row);
                var build = SkillBuild.Parse(row.Build);
                var reason = change(character, skills, build, row);
                if (reason == null)
                {
                    row.Build = build.ToString();
                    row.Updated = DateTime.UtcNow;
                }
                var info = Info(character, SkillBuild.Parse(row.Build), row);
                if (reason == null) TellGame(character, info);
                return new SkillBuildResult { Success = reason == null, Message = reason, Info = info };
            }
        }

        // a change made anywhere (the website, chat) reaches the game the character plays in, which follows the build
        private void TellGame(Character character, SkillBuildInfo info)
        {
            var session = gameData.GetSessionByCharacterId(character.Id);
            if (session == null) return;
            gameData.EnqueueGameEvent(gameData.CreateSessionEvent(GameEventType.SkillBuildChanged, session, info));
        }

        private CharacterSkillBuild GetOrCreate(Guid characterId)
        {
            var row = gameData.GetCharacterSkillBuild(characterId);
            if (row != null) return row;
            row = new CharacterSkillBuild { Id = Guid.NewGuid(), CharacterId = characterId, Build = "", FreeRespecs = 1, TreeVersion = SkillTrees.Version, Updated = DateTime.UtcNow };
            gameData.Add(row);
            return row;
        }

        private bool TakeTome(Character character)
        {
            var item = gameData.GetItems().FirstOrDefault(x => x.Name == TomeOfForgetting);
            if (item == null) return false;
            var inventory = inventoryProvider.Get(character.Id);
            if (inventory == null) return false;
            var stack = inventory.GetUnequippedItem(item.Id);
            return !stack.IsNull() && inventory.RemoveItem(stack, 1);
        }

        private long Tomes(Character character)
        {
            var item = gameData.GetItems().FirstOrDefault(x => x.Name == TomeOfForgetting);
            if (item == null) return 0;
            var inventory = inventoryProvider.Get(character.Id);
            if (inventory == null) return 0;
            var stack = inventory.GetUnequippedItem(item.Id);
            return stack.IsNull() ? 0 : stack.Amount;
        }

        private SkillBuildInfo Info(Character character, SkillBuild build, CharacterSkillBuild row)
        {
            var skills = gameData.GetCharacterSkills(character.SkillsId);
            return new SkillBuildInfo
            {
                CharacterId = character.Id,
                Build = build.ToString(),
                Earned = SkillTrees.EarnedPoints(TotalCombatLevels(skills)),
                Spent = build.Spent,
                FreeRespecs = row?.FreeRespecs ?? 1,
                RespecCost = SkillTrees.RespecCost(build.Spent),
                TomesOfForgetting = (int)Math.Min(int.MaxValue, Tomes(character)),
            };
        }
    }
}
