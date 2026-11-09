using RavenNest.Combat;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace RavenNest.CombatSimulator
{
    /// <summary>
    ///     Gear for the island enemies (the game's docs/combat-v2-design.md, enemy tuning): each kind gets the power,
    ///     accuracy and armour of the best melee kit at its own combat level, times a share. The table shows a player at
    ///     the fighting area's requirement and a player at the enemy's own level against it, and the proposal is written
    ///     to data/enemy-gear.json for the game (tools/combat/apply-enemy-gear.cs). Targets: an enemy at your level in 4
    ///     to 8 swings, and minutes of fighting at the right island without falling.
    /// </summary>
    public static class EnemyGearScenarios
    {
        public static void Run(GearPicker allGear, string path, int fights, double share, double offence)
        {
            var gear = allGear.Standard();
            var definitions = EnemyScenarios.Load(path).Where(d => !d.Scaled).OrderBy(EnemyScenarios.Requirement).ThenBy(d => d.CombatLevel).ToList();
            Console.WriteLine($"Enemies with {share:P0} of the armour and {offence:P0} of the power and accuracy of the standard melee kit at their own combat level;");
            Console.WriteLine($"players in the standard kit for their level. {fights:N0} fights per cell, new rules.");
            Console.WriteLine("Each side: swings to win, the player's health left, fights lost.");
            Console.WriteLine();
            Console.WriteLine($"{"Enemy",-26} {"Cb",4} {"Pow",4} {"Acc",4} {"Arm",5} | {"player at area req",-26} | {"player at enemy's level",-26}");
            Console.WriteLine(new string('-', 108));

            var random = new SeededCombatRandom(31);
            var proposal = new List<object>();
            foreach (var d in definitions)
            {
                var kit = gear.Melee(Math.Max(1, d.CombatLevel));
                var geared = d.Copy();
                geared.Power = (int)(kit.WeaponPower * offence);
                geared.Accuracy = (int)(kit.WeaponAim * offence);
                geared.Armor = (int)(kit.ArmorPower * share);
                proposal.Add(new { geared.Id, geared.Power, geared.Accuracy, geared.Armor });

                var req = EnemyScenarios.Requirement(d);
                var atReq = Cell(gear, req, geared, fights, random);
                var atOwn = Cell(gear, Math.Max(1, d.CombatLevel), geared, fights, random);
                Console.WriteLine($"{(d.Id.Length > 26 ? d.Id.Substring(0, 26) : d.Id),-26} {d.CombatLevel,4} {geared.Power,4} {geared.Accuracy,4} {geared.Armor,5} | {atReq,-26} | {atOwn,-26}");
            }

            var output = Path.Combine(AppContext.BaseDirectory, "data", "enemy-gear.json");
            File.WriteAllText(output, JsonSerializer.Serialize(proposal, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine();
            Console.WriteLine("Proposal written to " + output);
        }

        private static string Cell(GearPicker gear, int level, EnemyDefinition d, int fights, ICombatRandom random)
        {
            var kit = gear.Melee(level);
            double swings = 0, left = 0;
            var won = 0;
            for (var i = 0; i < fights; ++i)
            {
                var player = new CombatSheet
                {
                    CombatLevel = level,
                    MaxHealth = CombatMath.PlayerMaxHealth(EnemyScenarios.HealthLevel(level), kit.ArmorHealth),
                    DefenseLevel = level,
                    Armor = kit.ArmorPower,
                    IsPlayer = true,
                };
                player.Melee.DamageLevel = level;
                player.Melee.AimLevel = level;
                player.Melee.Power = kit.WeaponPower;
                player.Melee.Accuracy = kit.WeaponAim;
                kit.ApplyExtras(player);
                var enemy = d.Sheet();
                d.ApplyElement(enemy);
                var o = EnemyScenarios.Fight(player.MaxHealth, enemy.MaxHealth, EnemyScenarios.Interval(d),
                    () => CombatMath.Attack(player, AttackStyle.Melee, enemy, random).Total,
                    () => CombatMath.Attack(enemy, d.Style, player, random).Total);
                if (!o.Won) continue;
                won++;
                swings += o.Swings;
                left += o.HealthLeft;
            }
            var lost = 100.0 * (fights - won) / fights;
            return won == 0 ? $"lost all (lv {level})" : $"{swings / won,5:0.0} sw {left / won * 100,4:0}% hp {lost,3:0}% lost";
        }
    }
}
