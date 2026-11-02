using System;
using System.Collections.Generic;
using System.Linq;
using RavenNest.Models;
using Skills = RavenNest.DataModels.Skills;

namespace RavenNest.BusinessLogic.Game
{
    /// <summary>
    /// What the Ravebridge island's quests pay a streamer's real character (the game's StreamingAssets/Ravebridge/
    /// quests.json, the same keys; the game tracks the steps, the server pays). Decided by the user after the
    /// 2026-11-02 playtest: the rewards scale with the character as the dailies do, since the old fixed amounts (3,000
    /// Attack exp) were nothing to a high level character. Each skill's share of its next level is the quest's weight
    /// for it (the old amounts, the biggest one counting as 1) times <see cref="StoryShare"/>, or for a bounty, which
    /// comes round again every few minutes, <see cref="BountyShare"/> per ten minutes of its cooldown. Coins as a daily's,
    /// times the old coins over 300; bounties pay Ravebridge coins only (the game's). The game works out the same
    /// numbers to show them (RavebridgeQuests.ScaledExp); these are the ones paid.
    /// </summary>
    public static class RavebridgeQuestRewards
    {
        public const double StoryShare = 0.15;
        public const double BountyShare = 0.01;

        public sealed class Quest
        {
            public string Key;
            public bool Bounty;
            public double CooldownMinutes;
            public long Coins;
            public (Skill Skill, double Weight)[] Exp;
        }

        private static Quest Story(string key, long coins, params (Skill, double)[] exp) => new Quest { Key = key, Coins = coins, Exp = exp };
        private static Quest Bounty(string key, double cooldown, params (Skill, double)[] exp) => new Quest { Key = key, Bounty = true, CooldownMinutes = cooldown, Exp = exp };

        private static readonly Dictionary<string, Quest> quests = new[]
        {
            Story("defend", 500, (Skill.Attack, 3000), (Skill.Defense, 3000), (Skill.Strength, 3000), (Skill.Health, 2000)),
            Story("windmill", 200, (Skill.Crafting, 1500), (Skill.Attack, 600)),
            Story("cook", 300, (Skill.Cooking, 2000)),
            Story("ghost", 250, (Skill.Healing, 1500), (Skill.Magic, 1000)),
            Story("goblins", 300, (Skill.Crafting, 1200), (Skill.Strength, 1200)),
            Story("bridge", 250, (Skill.Woodcutting, 1500), (Skill.Mining, 800)),
            Story("talisman", 250, (Skill.Magic, 1800), (Skill.Mining, 900)),
            Story("romance", 300, (Skill.Health, 1500), (Skill.Strength, 800), (Skill.Gathering, 800)),
            Story("cows", 200, (Skill.Attack, 800), (Skill.Slayer, 800)),
            Story("sauce", 300, (Skill.Cooking, 2200), (Skill.Fishing, 600)),
            Story("vampire", 400, (Skill.Attack, 2000), (Skill.Strength, 2000), (Skill.Magic, 1000), (Skill.Healing, 1000)),
            Bounty("ears", 10, (Skill.Attack, 400), (Skill.Strength, 400), (Skill.Defense, 400)),
            Bounty("teeth", 10, (Skill.Attack, 300), (Skill.Strength, 300), (Skill.Defense, 300)),
            Bounty("bones", 10, (Skill.Healing, 700)),
            Bounty("goo", 15, (Skill.Magic, 800)),
            Bounty("stew", 10, (Skill.Cooking, 500)),
            Bounty("sails", 10, (Skill.Crafting, 500)),
            Bounty("tusk", 30, (Skill.Attack, 1500), (Skill.Strength, 1500), (Skill.Defense, 1500), (Skill.Health, 1000)),
        }.ToDictionary(q => q.Key);

        public static Quest Get(string key) => key != null && quests.TryGetValue(key, out var q) ? q : null;

        /// <summary>The experience for each skill (its index), from the character's levels now.</summary>
        public static Dictionary<int, double> Exp(Quest quest, Skills skills)
        {
            var result = new Dictionary<int, double>();
            if (quest?.Exp == null || quest.Exp.Length == 0 || skills == null) return result;
            var biggest = quest.Exp.Max(e => e.Weight);
            var share = quest.Bounty ? BountyShare * quest.CooldownMinutes / 10.0 : StoryShare;
            foreach (var (skill, weight) in quest.Exp)
            {
                var level = skills.GetSkill((int)skill)?.Level ?? 1;
                var exp = Math.Round(GameMath.ExperienceForLevel(Math.Min(level + 1, GameMath.MaxLevel)) * share * weight / biggest);
                if (exp > 0) result[(int)skill] = exp;
            }
            return result;
        }

        public static long Coins(Quest quest, Skills skills)
        {
            if (quest == null || quest.Coins <= 0 || skills == null) return 0;
            var average = skills.GetSkills().Average(x => x.Level);
            return (long)Math.Round((300 + average * 15) * (quest.Coins / 300.0) / 10) * 10;
        }
    }

    /// <summary>What a Ravebridge quest paid (api/quests/{character}/ravebridge/{key}): the game adds the experience.</summary>
    public class RavebridgeQuestReward
    {
        public bool Ok { get; set; }
        /// <summary>notinsession, unknown (no such quest), done (a quest already paid), cooldown (a bounty too soon).</summary>
        public string Error { get; set; }
        public long Coins { get; set; }
        /// <summary>Experience per skill index.</summary>
        public Dictionary<int, double> Exp { get; set; } = new Dictionary<int, double>();
    }
}
