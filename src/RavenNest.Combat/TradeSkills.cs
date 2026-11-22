using System;

namespace RavenNest.Combat
{
    /// <summary>What a character's trade nodes do for one gathering round in one skill (TradeSkills.Gathering).</summary>
    public struct GatherBonus
    {
        /// <summary>Sure Hands: added to the chance of every find (0.10 is ten points more).</summary>
        public double FindChance;

        /// <summary>Village's Share: the chance that a round gives the village one more of its resource.</summary>
        public double VillageShare;

        /// <summary>Bountiful: the chance that a find is two (added to a gatherer pet's Forage).</summary>
        public double DoubleFind;

        /// <summary>Tireless: the chance that the round counts twice (twice that with the capstone, in a skill at 999).</summary>
        public double DoubleRound;

        /// <summary>Quick Return: the share taken off every find's wait.</summary>
        public double WaitCut;

        /// <summary>Nest Finder: a small chance of a pet egg each round.</summary>
        public bool Eggs;

        /// <summary>Master of the Trade, in a skill at 999: each round is worth two to the village.</summary>
        public bool VillageTwice;

        public bool IsEmpty => FindChance <= 0 && VillageShare <= 0 && DoubleFind <= 0 && DoubleRound <= 0 && WaitCut <= 0 && !Eggs && !VillageTwice;

        /// <summary>What a round gives the village: one, or two with the capstone.</summary>
        public int VillagePerRound => VillageTwice ? 2 : 1;
    }

    /// <summary>What a character's trade nodes do for one item made with one skill (TradeSkills.Making).</summary>
    public struct MakeBonus
    {
        /// <summary>Steady Hands: added to the success chance of a recipe that can fail.</summary>
        public double Success;

        /// <summary>Thrifty: the chance that the make uses up no ingredients (they must still be owned).</summary>
        public double KeepIngredients;

        /// <summary>Second Helping: the chance of one more of what was made (Cooking and Alchemy only).</summary>
        public double ExtraPortion;

        public bool IsEmpty => Success <= 0 && KeepIngredients <= 0 && ExtraPortion <= 0;
    }

    /// <summary>
    ///     The trade trees' numbers (the game's docs/trade-skills-design.md): what a build's Gathering and Artisan
    ///     nodes are worth in a skill at a level. The server rolls them where the find, the make, the gem or the
    ///     enchant is already rolled; nothing here runs in a fight. A node counts in a skill only once that skill is
    ///     at the node's level (SkillBuild.RankIn), so Bountiful (level 100) works while mining with Mining 100 and
    ///     not while fishing with Fishing 40.
    /// </summary>
    public static class TradeSkills
    {
        /// <summary>Nest Finder: the chance of an egg in one gathering round (10 s): about one in 20 hours.</summary>
        public const double EggChancePerRound = 1.0 / 7200;

        /// <summary>The level a skill must have for the capstones to count in it.</summary>
        public const int MasterLevel = 999;

        /// <summary>A build's Gathering nodes in one gathering skill at this level (the skill's own level, without gear).</summary>
        public static GatherBonus Gathering(SkillBuild build, int skillLevel)
        {
            var bonus = new GatherBonus();
            if (build == null || build.Ranks.Count == 0) return bonus;
            var master = false;
            foreach (var node in SkillTrees.TradeNodes(SkillTree.Gathering))
            {
                var rank = build.RankIn(node, skillLevel);
                if (rank <= 0) continue;
                switch (node.Trade)
                {
                    case TradeEffect.FindChance: bonus.FindChance += node.TradeAmount * rank; break;
                    case TradeEffect.VillageShare: bonus.VillageShare += node.TradeAmount * rank; break;
                    case TradeEffect.DoubleFind: bonus.DoubleFind += node.TradeAmount * rank; break;
                    case TradeEffect.DoubleRound: bonus.DoubleRound += node.TradeAmount * rank; break;
                    case TradeEffect.FindWait: bonus.WaitCut += node.TradeAmount * rank; break;
                    case TradeEffect.Eggs: bonus.Eggs = true; break;
                    case TradeEffect.GatheringMaster: master = true; break;
                }
            }
            if (master)
            {
                bonus.VillageTwice = true;
                bonus.DoubleRound *= 2;
            }
            return bonus;
        }

        /// <summary>
        ///     A build's Artisan nodes for one item made with Crafting, Cooking or Alchemy at this level (the skill's
        ///     own level, without gear). Second Helping is for meals and potions only.
        /// </summary>
        public static MakeBonus Making(SkillBuild build, CombatSkill skill, int skillLevel)
        {
            var bonus = new MakeBonus();
            if (build == null || build.Ranks.Count == 0) return bonus;
            var master = 0d;
            foreach (var node in SkillTrees.TradeNodes(SkillTree.Artisan))
            {
                if (node.Skill.HasValue) continue; // the gem and enchanting nodes: not about making
                var rank = build.RankIn(node, skillLevel);
                if (rank <= 0) continue;
                switch (node.Trade)
                {
                    case TradeEffect.CraftSuccess: bonus.Success += node.TradeAmount * rank; break;
                    case TradeEffect.KeepIngredients: bonus.KeepIngredients += node.TradeAmount * rank; break;
                    case TradeEffect.ExtraPortion: if (skill != CombatSkill.Crafting) bonus.ExtraPortion += node.TradeAmount * rank; break;
                    case TradeEffect.ArtisanMaster: master = node.TradeAmount; break;
                }
            }
            // the capstone makes the two worth more where they are learned; it gives nothing by itself
            if (master > 0)
            {
                if (bonus.KeepIngredients > 0) bonus.KeepIngredients += master;
                if (bonus.ExtraPortion > 0) bonus.ExtraPortion += master;
            }
            return bonus;
        }

        /// <summary>A find's chance with Sure Hands: the points are added, before a gatherer pet's Keen Nose multiplies.</summary>
        public static double FindChance(double chance, GatherBonus bonus) => chance + Math.Max(0, bonus.FindChance);

        /// <summary>A find's wait in seconds with Quick Return.</summary>
        public static double FindWait(double seconds, GatherBonus bonus) => seconds <= 0 ? seconds : seconds * (1 - Math.Min(0.9, Math.Max(0, bonus.WaitCut)));

        /// <summary>A recipe's success chance with Steady Hands, never past certain.</summary>
        public static double SuccessChance(double chance, MakeBonus bonus) => Math.Min(1.0, chance + Math.Max(0, bonus.Success));

        /// <summary>Gem Cutter: the share taken off the fee for combining gems.</summary>
        public static double CombineDiscount(SkillBuild build) => Total(build, TradeEffect.CombineDiscount);

        /// <summary>What combining gems costs with Gem Cutter: 1,000 coins become 700 at five ranks. Whole coins, never less than one.</summary>
        public static long CombineFee(long fee, SkillBuild build)
        {
            var discount = CombineDiscount(build);
            if (discount <= 0 || fee <= 0) return fee;
            // in hundredths, so 6% of 1,000 is 60 and not 59.99...
            var hundredths = (long)Math.Round(Math.Min(0.9, discount) * 100);
            return Math.Max(1, fee - fee * hundredths / 100);
        }

        /// <summary>Enchanter's Patience: what is left of the wait between two enchants (0.85 at five ranks).</summary>
        public static double EnchantWaitFactor(SkillBuild build) => 1 - Math.Min(0.9, Total(build, TradeEffect.EnchantWait));

        // the nodes opened by one skill alone (Crafting): held means the level was there, levels do not go down
        private static double Total(SkillBuild build, TradeEffect effect)
        {
            if (build == null || build.Ranks.Count == 0) return 0;
            var total = 0d;
            foreach (var node in SkillTrees.TradeNodes(SkillTree.Artisan))
                if (node.Trade == effect) total += node.TradeAmount * Math.Min(build.Rank(node.Id), node.MaxRank);
            return total;
        }
    }
}
