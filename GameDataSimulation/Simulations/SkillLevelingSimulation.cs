namespace GameDataSimulation
{
    public class SkillLevelingSimulationSettings : SimulationSettings
    {
        public int PlayersInArea = 100;
        public int NextLevel = 50;

        /// <summary>
        /// 0 to 1.0, exp multiplier factor, 0 being no effect from the multiplier. 1 being full effect.<br/>
        /// This could be controlled by island or other types of events.
        /// </summary>
        public double MultiplierFactor = 1.0; // 0 to 1.0, multiplier factor, islands could control this.

        /// <summary>
        /// 0 to 1.0, exp factor, 0 being no exp. Calculated based on current level and target level.<br/>
        /// If player level is too high compared to the target level, the exp amount is dropped. <br/> <br/>
        /// Could also be related to what island a player is mostly suited for. <br/> <br/>
        /// ■ If player can go to Away, training on home will yield less exp. <br/>
        /// ■ If player can go to Ironhill, Away will yield less exp. <br/>
        /// ■ etc..
        /// </summary>
        public double ExpFactor = 1.0;
        /// <summary>
        /// Active exp multiplier, this is including rested, global boost and local boosts.
        /// </summary>
        public double ExpBoost = 1.0;
        public Skill Skill;
        public double? Exp;
        public ExpGainType ExpGainType = ExpGainType.Fixed;
    }

    public enum ExpGainType
    {
        Fixed,
        FixedMin,
        FixedMinMax,
    }

    public class SkillLevelingSimulation : ISimulation
    {
        public SimulationResult Run(ISimulationSettings settings)
        {
            var result = new SkillLevelSimulationResult();
            var simSettings = (SkillLevelingSimulationSettings)settings;

            var nextLevel = simSettings.NextLevel;
            var skill = simSettings.Skill;
            var playersInArea = simSettings.PlayersInArea;
            var targetLevel = simSettings.MultiplierFactor;
            var boost = simSettings.ExpBoost;

            var value = GameMath.Exp.GetMinExpGainPercent(nextLevel, Skill.Woodcutting, playersInArea);
            Console.WriteLine("╔═══════════════╦═══════╦═══════════════════════╗");
            Console.WriteLine("║ Skill\t\t║ Value\t║ Secondary             ║");
            Console.WriteLine("╠═══════════════╬═══════╬═══════════════════════╣");
            Console.WriteLine("║ " + "Gain".PadRight(13) + "\t║ -\t║ " + simSettings.ExpGainType.ToString().PadRight(22, ' ') + "║");
            Console.WriteLine("║ " + "M. Factor".PadRight(13) + "\t║ " + simSettings.MultiplierFactor + "\t║ " + "-".PadRight(22, ' ') + "║");
            Console.WriteLine("║ " + "XP Factor".PadRight(13) + "\t║ " + simSettings.ExpFactor + "\t║ " + "-".PadRight(22, ' ') + "║");
            Console.WriteLine("║ " + "Boost".PadRight(13) + "\t║ " + boost + "\t║ " + "-".PadRight(22, ' ') + "║");
            Console.WriteLine("║ " + "Players".PadRight(13) + "\t║ " + playersInArea + "\t║ " + "-".PadRight(22, ' ') + "║");
            Console.WriteLine("║ " + skill.ToString().PadRight(13) + "\t║ " + nextLevel + "\t║ " + value.ToString().PadRight(22, ' ') + "║");
            Console.WriteLine("╠═══════════════╩═══════╩═══════════════════════╝");

            var ticksForLevel = GameMath.Exp.GetTotalTicksForLevel(nextLevel, skill, playersInArea);
            var ticksPerSeconds = GameMath.Exp.GetTicksPerSeconds(skill, playersInArea);
            var timeLeftToLevel = TimeSpan.FromSeconds(ticksForLevel / ticksPerSeconds);

            var effectiveBoost = GameMath.Exp.GetEffectiveExpMultiplier(nextLevel, boost);
            var bTicksForLevel = GameMath.Exp.GetTotalTicksForLevel(nextLevel, skill, boost, playersInArea);
            var bTimeLeftToLevel = TimeSpan.FromSeconds(bTicksForLevel / ticksPerSeconds);

            var expForNextLevel = GameMath.ExperienceForLevel(nextLevel);
            var maxExpGain = expForNextLevel / bTicksForLevel; // should this be min exp gain instead? 
            var minExpGainPercent = GameMath.Exp.GetMinExpGainPercent(nextLevel, skill, playersInArea);
            var minExpGain = GameMath.ExperienceForLevel(nextLevel) * minExpGainPercent;
            var expInput = simSettings.Exp ?? GameMath.GetWoodcuttingExperience((int)(nextLevel * simSettings.MultiplierFactor));
            var expRaw = expInput * effectiveBoost;

            double exp;
            switch (simSettings.ExpGainType)
            {
                case ExpGainType.Fixed:
                    exp = Lerp(0, Lerp(minExpGain, maxExpGain, simSettings.MultiplierFactor), simSettings.ExpFactor);
                    break;
                case ExpGainType.FixedMin:
                    exp = Math.Max(expInput, Math.Min(maxExpGain, Math.Max(expRaw, minExpGain)));
                    break;
                default:
                    exp = Math.Min(maxExpGain, Math.Max(expRaw, minExpGain));
                    break;
            }

            var percent = (exp / expForNextLevel) * 100;
            var percentPerSeconds = ticksPerSeconds * (percent / 100.0);
            var timeLeft = TimeSpan.FromSeconds(1.0 / percentPerSeconds);

            Console.WriteLine("║ »                  Default                   «");
            Console.WriteLine("║------------------------------------------------");
            Console.WriteLine("║ Ticks Per Seconds: " + ticksPerSeconds);
            Console.WriteLine("║ Ticks For Level:   " + ticksForLevel);
            Console.WriteLine("║ Time For Level:    " + timeLeftToLevel);
            Console.WriteLine("╠════════════════════════════════════════════════");
            Console.WriteLine("║ »                 With Boost                 «");
            Console.WriteLine("║------------------------------------------------");
            Console.WriteLine("║ Effective Boost:   x" + effectiveBoost);
            Console.WriteLine("║ Ticks For Level:   " + bTicksForLevel);
            Console.WriteLine("╠═══════════════════════════════════════════════╗");
            Console.WriteLine("║ Time For Level: " + bTimeLeftToLevel.ToString().PadRight(30) + "║");
            Console.WriteLine("╚═══════════════════════════════════════════════╝");
            Console.WriteLine();
            Console.WriteLine("   Exp For Level:  " + expForNextLevel);
            if (simSettings.ExpGainType != ExpGainType.Fixed)
            {
                Console.WriteLine("   Max Exp Gain:   " + maxExpGain);
                Console.WriteLine("   Min Exp Gain %: " + (minExpGainPercent * 100) + "%");
                Console.WriteLine("   Min Exp Gain:   " + minExpGain);
                Console.WriteLine("   Exp Input:      " + expInput);
                Console.WriteLine("   Exp Raw:        " + expRaw);
            }
            Console.WriteLine("   Gained Exp:     " + exp);
            Console.WriteLine("   Increment %:    " + percent + "%");
            Console.WriteLine("   % Per Sec:      " + (percentPerSeconds * 100.0) + "%");
            Console.WriteLine("   Time Left:      " + timeLeft);

            return result;
        }

        private static double Lerp(double v1, double v2, double t)
        {
            return v1 + (v2 - v1) * t;
        }

        public class SkillLevelSimulationResult : SimulationResult { }
    }
}
