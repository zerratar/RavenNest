using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.Combat;
using RavenNest.DataModels;
using RavenNest.Models;
using GameSession = RavenNest.DataModels.GameSession;
using Skills = RavenNest.DataModels.Skills;
using Character = RavenNest.DataModels.Character;

namespace RavenNest.BusinessLogic.Game
{
    /// <summary>
    ///     The Legendary tier on the server (the game's docs/legendary-design.md): who qualifies, the roll with its quiet
    ///     bad-luck count when a boss falls, the find itself (soulbound, a fresh awakening tag), awakening the worn ones
    ///     from boss kills, the hidden titles and what the Hall of Legends shows. The rules and numbers are
    ///     RavenNest.Combat.Legendary's; the game only shows what this decides (the item comes with the rewards, the
    ///     awakening count on the inventory item's tag with the inventory feed).
    /// </summary>
    public class LegendaryManager
    {
        /// <summary>The hidden achievement: different Legendaries found (tier 1 at one, tier 2 at all of them).</summary>
        public const string Achievement = "legend";

        private readonly GameData gameData;
        private readonly PlayerInventoryProvider inventories;
        private readonly ILogger<LegendaryManager> logger;
        private readonly object mutex = new object();
        private readonly Random rng = new Random();

        public LegendaryManager(GameData gameData, PlayerInventoryProvider inventories, ILogger<LegendaryManager> logger)
        {
            this.gameData = gameData;
            this.inventories = inventories;
            this.logger = logger;
        }

        /// <summary>
        ///     A boss fell with these characters in the fight. Every one of them wearing a Legendary awakens it a little;
        ///     when the boss is a source (<paramref name="source"/> not null) every one who qualifies rolls for a find.
        ///     The finds come back as rewards for the game to show.
        /// </summary>
        public EventItemReward[] BossFell(GameSession session, Guid[] characterIds, LegendarySource? source)
        {
            var rewards = new List<EventItemReward>();
            if (session == null || characterIds == null || characterIds.Length == 0) return rewards.ToArray();
            var sessionCharacters = gameData.GetActiveSessionCharacters(session);
            foreach (var id in characterIds.Distinct())
            {
                var character = sessionCharacters.FirstOrDefault(x => x.Id == id);
                if (character == null) continue;
                try
                {
                    Awaken(character);
                    if (source.HasValue)
                    {
                        var found = Roll(character, source.Value);
                        if (found != null) rewards.Add(found);
                    }
                }
                catch (Exception exc)
                {
                    logger.LogError("Legendary: a boss kill for " + character.Name + " failed: " + exc);
                }
            }
            if (rewards.Count > 0 || source.HasValue) gameData.Touch();
            return rewards.ToArray();
        }

        // ---- who qualifies --------------------------------------------------------------------------------------------

        /// <summary>The highest part of the tower whose clear the character fought, as a step (the climber achievement), 0 for none.</summary>
        public int HighestClearFought(Guid characterId)
        {
            var climber = gameData.GetCharacterAchievements(characterId)?.FirstOrDefault(a => a.Key == "climber");
            return climber != null ? (int)Math.Min(int.MaxValue, climber.Progress) : 0;
        }

        private static int LevelOf(Skills skills, LegendarySkill skill)
        {
            if (skills == null) return 0;
            switch (skill)
            {
                case LegendarySkill.Attack: return skills.AttackLevel;
                case LegendarySkill.Defense: return skills.DefenseLevel;
                case LegendarySkill.Magic: return skills.MagicLevel;
                case LegendarySkill.Strength: return skills.StrengthLevel;
                default: return skills.RangedLevel;
            }
        }

        /// <summary>The Legendaries a character could find now (section 2 of the design).</summary>
        public IReadOnlyList<LegendaryInfo> Findable(Character character) => Qualified(character, false);

        /// <summary>
        ///     The Legendaries a character qualifies for: its skill, the tower and the ending; with
        ///     <paramref name="evenOwned"/> those it owns already count too (Legend's Echo).
        /// </summary>
        public IReadOnlyList<LegendaryInfo> Qualified(Character character, bool evenOwned)
        {
            var list = new List<LegendaryInfo>();
            if (character == null) return list;
            var skills = gameData.GetCharacterSkills(character.SkillsId);
            var floor = HighestClearFought(character.Id);
            var build = SkillTreeManager.BuildOf(gameData, character.Id);
            var owned = inventories.Get(character.Id).GetAllItems().Select(x => x.ItemId).ToHashSet();
            foreach (var l in Legendary.All)
            {
                if (Legendary.Qualifies(LevelOf(skills, l.FindSkill), floor, build?.CapstoneOf(l.Tree) != null, !evenOwned && owned.Contains(l.ItemId)))
                    list.Add(l);
            }
            return list;
        }

        // ---- the roll -------------------------------------------------------------------------------------------------

        private EventItemReward Roll(Character character, LegendarySource source)
        {
            // a character rolls when it qualifies for any Legendary; a find of one it owns already is a Legend's Echo
            var qualified = Qualified(character, true);
            if (qualified.Count == 0) return null; // nothing to find: the bad-luck count stands still
            var findable = Findable(character);

            LegendaryInfo legendary;
            lock (mutex)
            {
                var row = gameData.GetCharacterLegendary(character.Id);
                if (row == null)
                {
                    row = new CharacterLegendary { Id = Guid.NewGuid(), CharacterId = character.Id };
                    gameData.Add(row);
                }

                var forced = Forced(character.Id);
                var roll = rng.NextDouble();
                if (!forced && !Legendary.Found(source, row.Kills, roll))
                {
                    row.Kills++;
                    row.Updated = DateTime.UtcNow;
                    return null;
                }

                legendary = findable.Count > 0 ? findable[Math.Min(findable.Count - 1, (int)(rng.NextDouble() * findable.Count))] : null;
                logger.LogWarning("Legendary: {Character} found {What} from a {Source} after {Kills} qualifying kill(s){Forced}.",
                    character.Name, legendary != null ? "the " + legendary.Name : "a Legend's Echo", source, row.Kills + 1, forced ? " (forced for a test)" : "");
                row.Kills = 0;
                row.Finds++;
                row.Updated = DateTime.UtcNow;
            }

            return legendary != null ? Give(character, legendary, (int)source) : Echo(character, qualified);
        }

        /// <summary>
        ///     Legend's Echo (section 3 of the design): the character owns every Legendary it qualifies for, so the find
        ///     awakens the least awakened of those by <see cref="Legendary.EchoKills"/> boss kills (the game sees the tag
        ///     and says so over the character, quietly); when every one is at rank 5, a Flawless gem instead.
        /// </summary>
        private EventItemReward Echo(Character character, IReadOnlyList<LegendaryInfo> qualified)
        {
            var inventory = inventories.Get(character.Id);
            var ids = qualified.Select(x => x.ItemId).ToHashSet();
            var stacks = inventory.GetAllItems().Where(x => ids.Contains(x.ItemId)).ToList();
            var target = Legendary.EchoTarget(stacks.Select(x => Legendary.KillsOf(x.Tag)).ToList());
            if (target >= 0)
            {
                var stack = inventory.Get(stacks[target]);
                if (stack == null) return null;
                var before = Legendary.KillsOf(stack.Tag);
                var after = before + Legendary.EchoKills;
                stack.Tag = Legendary.Tag(after); // the inventory feed tells the game
                var legendary = Legendary.Get(stack.ItemId);
                logger.LogInformation("Legendary: Legend's Echo, {Character}'s {Item} {Before} to {After} boss kills.", character.Name, legendary?.Name, before, after);
                if (legendary != null && Legendary.RankOf(after) >= Legendary.MaxRank && Legendary.RankOf(before) < Legendary.MaxRank)
                    FullyAwakened(character, legendary, stack.Id);
                return null;
            }

            var kinds = Enum.GetValues(typeof(GemKind));
            var kind = (GemKind)kinds.GetValue(rng.Next(kinds.Length));
            var gemId = Gems.ItemId(kind, Legendary.EchoGemTier);
            var gem = inventory.AddItem(gemId, 1).FirstOrDefault();
            if (gem == null) return null;
            logger.LogInformation("Legendary: Legend's Echo, {Character} has every Legendary at rank 5: a {Gem}.", character.Name, Gems.Name(kind, Legendary.EchoGemTier));
            return new EventItemReward { Amount = 1, CharacterId = character.Id, ItemId = gemId, InventoryItemId = gem.Id };
        }

        /// <summary>
        ///     Puts a Legendary in the character's inventory (soulbound, a fresh awakening tag), notes the find for the
        ///     Hall of Legends and counts it for the hidden titles. Also the admin's way to give one.
        /// </summary>
        public EventItemReward Give(Character character, LegendaryInfo legendary, int source)
        {
            var stack = inventories.Get(character.Id).AddItem(legendary.ItemId, 1, tag: Legendary.Tag(0), soulbound: true).FirstOrDefault();
            if (stack == null) return null;
            gameData.Add(new LegendaryFind
            {
                Id = Guid.NewGuid(),
                CharacterId = character.Id,
                ItemId = legendary.ItemId,
                InventoryItemId = stack.Id,
                Source = source,
                Found = DateTime.UtcNow,
            });
            var kinds = gameData.GetLegendaryFinds(character.Id).Select(x => x.ItemId).Distinct().Count();
            QuestTracker.Achievements?.Set(character, Achievement, kinds);
            return new EventItemReward { Amount = 1, CharacterId = character.Id, ItemId = legendary.ItemId, InventoryItemId = stack.Id };
        }

        /// <summary>
        ///     Development only: the ids in generated-data/legendary-force.txt find one at their next qualifying boss
        ///     kill (the id is taken off the list), so a find can be tested without hundreds of raids.
        /// </summary>
        private bool Forced(Guid characterId) => TakeTestLine("legendary-force.txt", characterId, out _);

        /// <summary>
        ///     Development only: a line "id kills" in generated-data/legendary-awaken.txt sets the character's worn
        ///     Legendaries to that many boss kills at its next boss kill (before it counts), so ranks can be tested.
        /// </summary>
        private int? TestKills(Guid characterId) => TakeTestLine("legendary-awaken.txt", characterId, out var rest) && int.TryParse(rest, out var n) ? n : (int?)null;

        private static bool TakeTestLine(string file, Guid characterId, out string rest)
        {
            rest = null;
            try
            {
                if (!string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase)) return false;
                var path = Path.Combine("generated-data", file);
                if (!File.Exists(path)) return false;
                var lines = File.ReadAllLines(path).Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
                var mine = lines.FindIndex(x => Guid.TryParse(x.Split(' ')[0], out var g) && g == characterId);
                if (mine < 0) return false;
                var parts = lines[mine].Split(' ', 2);
                rest = parts.Length > 1 ? parts[1].Trim() : null;
                lines.RemoveAt(mine);
                File.WriteAllLines(path, lines);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // ---- awakening ------------------------------------------------------------------------------------------------

        /// <summary>A boss fell with the character there: each Legendary it wears counts one kill on its tag.</summary>
        private void Awaken(Character character)
        {
            var inventory = inventories.Get(character.Id);
            var worn = inventory.GetEquippedItems().Where(x => Legendary.IsLegendary(x.ItemId)).ToList();
            if (worn.Count == 0) return;
            var testKills = TestKills(character.Id);
            foreach (var item in worn)
            {
                var legendary = Legendary.Get(item.ItemId);
                var stack = inventory.Get(item);
                if (stack == null) continue;
                var before = Legendary.KillsOf(stack.Tag);
                if (testKills.HasValue)
                {
                    before = Math.Max(0, testKills.Value);
                    logger.LogWarning("Legendary: {Character}'s {Item} set to {Kills} boss kills for a test.", character.Name, legendary.Name, before);
                }
                var after = before + 1;
                stack.Tag = Legendary.Tag(after); // the inventory feed tells the game
                if (Legendary.RankOf(after) > Legendary.RankOf(before))
                {
                    logger.LogInformation("Legendary: {Character}'s {Item} awakened to rank {Rank}.", character.Name, legendary.Name, Legendary.RankOf(after));
                    if (Legendary.RankOf(after) >= Legendary.MaxRank) FullyAwakened(character, legendary, stack.Id);
                }
            }
        }

        private void FullyAwakened(Character character, LegendaryInfo legendary, Guid inventoryItemId)
        {
            var find = gameData.GetLegendaryFinds(character.Id).Where(x => x.ItemId == legendary.ItemId).OrderByDescending(x => x.InventoryItemId == inventoryItemId).ThenBy(x => x.Found).FirstOrDefault();
            if (find == null)
            {
                // one given before the finds were noted (an admin, a test): noted now
                find = new LegendaryFind { Id = Guid.NewGuid(), CharacterId = character.Id, ItemId = legendary.ItemId, InventoryItemId = inventoryItemId, Source = -1, Found = DateTime.UtcNow };
                gameData.Add(find);
            }
            if (find.Awakened == null) find.Awakened = DateTime.UtcNow;
        }

        /// <summary>The awakening count of an inventory item (0 when it is no Legendary).</summary>
        public static int KillsOf(ReadOnlyInventoryItem item) => Legendary.IsLegendary(item.ItemId) ? Legendary.KillsOf(item.Tag) : 0;

        // ---- the Hall of Legends --------------------------------------------------------------------------------------

        public sealed class HallEntry
        {
            public LegendaryInfo Legendary;
            public string FirstFinder;
            public DateTime? FirstFound;
            public int Found;
            public List<(string Name, DateTime Awakened)> Awakened = new List<(string, DateTime)>();
        }

        /// <summary>For each Legendary: who found it first in the world, how many were found, who fully awakened one.</summary>
        public IReadOnlyList<HallEntry> Hall()
        {
            var finds = gameData.GetLegendaryFinds();
            var hall = new List<HallEntry>();
            foreach (var l in Legendary.All)
            {
                var mine = finds.Where(x => x.ItemId == l.ItemId).ToList();
                var first = mine.OrderBy(x => x.Found).FirstOrDefault();
                var entry = new HallEntry
                {
                    Legendary = l,
                    Found = mine.Select(x => x.CharacterId).Distinct().Count(),
                    FirstFinder = first != null ? NameOf(first.CharacterId) : null,
                    FirstFound = first?.Found,
                };
                foreach (var a in mine.Where(x => x.Awakened != null).OrderBy(x => x.Awakened))
                {
                    var name = NameOf(a.CharacterId);
                    if (!entry.Awakened.Any(x => x.Name == name)) entry.Awakened.Add((name, a.Awakened.Value));
                }
                hall.Add(entry);
            }
            return hall;
        }

        private string NameOf(Guid characterId)
        {
            var c = gameData.GetCharacter(characterId);
            if (c == null) return "?";
            if (!string.IsNullOrEmpty(c.Name)) return c.Name;
            return gameData.GetUser(c.UserId)?.UserName ?? "?";
        }
    }
}
