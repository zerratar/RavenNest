using System;

namespace RavenNest.CombatSimulator
{
    /// <summary>
    ///     Today's melee blow, ported from the game (Assets/Scripts/Core/GameMath.cs: CalculateMeleeDamage and
    ///     CalculateDamage, 2026-11-07) as the baseline the new rules are compared with. UnityEngine.Random.Range(int,
    ///     int) excludes the upper bound, as Random.Next does. No dodge, no crit (players have none by default),
    ///     StyleBonus is 0 in the game.
    /// </summary>
    public static class TodaysCombat
    {
        public sealed class Fighter
        {
            public int Attack, Strength, Defense, Health;
            public int WeaponAim, WeaponPower, ArmorPower;
            public bool IsEnemy;
        }

        public static int MaxHit(int level, int power)
        {
            var w1 = power * 0.00175D;
            var w2 = w1 + 0.1d;
            var w3 = (level + 3) * w2 + 1.05D;
            return (int)(w3 * 0.95d);
        }

        public static float MeleeBlow(Fighter attacker, Fighter defender, Random random)
        {
            // CalculateMeleeDamage: damage skill = Strength (StrengthMultiplier 1), aim skill = Attack, the defender's
            // damage skill = Strength, minHitChance 40
            var attackerDamageSkill = attacker.Strength;
            var attackerAimSkill = attacker.Attack;
            var defenderDamageSkill = defender.Strength;

            var max = MaxHit(attackerDamageSkill, attacker.WeaponPower);
            var newAtt = (int)((attackerAimSkill / 0.8D) + attacker.WeaponAim + (attackerDamageSkill / 5D) + 10);
            var newDef = (int)((((random.Next(0, 100) <= 5 ? 0 : defender.Defense) * 1.1D)
                         + ((random.Next(0, 100) <= 5 ? 0 : defender.ArmorPower) / 2.75D)
                         + (defenderDamageSkill / 4D)));

            var hitChance = random.Next(0, 100) + (newAtt - newDef);
            if (attacker.IsEnemy) hitChance -= 5;
            if (random.Next(0, 100) <= 10) hitChance += 20;

            var reqHitChance = Math.Min(40, defender.IsEnemy ? 40 : 50);
            if (hitChance <= reqHitChance) return 0;

            var maxProb = 5;
            var nearMaxProp = 7;
            var avProb = 73;
            var defenseRange = (int)Math.Round(defender.ArmorPower * 0.02d, MidpointRounding.AwayFromZero);
            maxProb -= defenseRange;
            nearMaxProp -= (int)Math.Round(defenseRange * 1.5, MidpointRounding.AwayFromZero);
            avProb -= (int)Math.Round(defenseRange * 2.0, MidpointRounding.AwayFromZero);

            var hitRange = random.Next(0, 100);
            if (hitRange >= (100 - maxProb)) return max;
            if (hitRange >= (100 - nearMaxProp))
                return (int)Math.Round(Math.Abs(max - max * (random.Next(0, 10) * 0.01D)), MidpointRounding.AwayFromZero);
            int newMax;
            if (hitRange >= (100 - avProb))
            {
                newMax = (int)Math.Round(max - (max * 0.1D));
                return (int)Math.Round(Math.Abs(newMax - newMax * (random.Next(0, 50) * 0.01D)), MidpointRounding.AwayFromZero);
            }

            newMax = (int)Math.Round(max - max * 0.5D);
            return (int)Math.Round(Math.Abs(newMax - newMax * (random.Next(0, 50) * 0.01D)), MidpointRounding.AwayFromZero);
        }
    }
}
