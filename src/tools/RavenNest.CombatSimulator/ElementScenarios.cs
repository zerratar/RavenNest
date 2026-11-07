using RavenNest.Combat;
using System;
using System.IO;
using System.Linq;

namespace RavenNest.CombatSimulator
{
    /// <summary>
    ///     What each element does against each island enemy: the player of the enemies scenario (at the area's
    ///     requirement, best melee gear) with the weapon's hits a quarter fire, ice, lightning or poison (a themed
    ///     weapon, see the server's GameData.GearStats), against the enemies' resistances from their definitions.
    ///     Twice: the enemies as they are (no armour, so physical damage is never reduced), and in armour like the
    ///     player's (what enemy gear tuning would give them).
    /// </summary>
    public static class ElementScenarios
    {
        private const double Share = 0.25;

        public static void Run(GearPicker gear, string path, int fights)
        {
            var definitions = EnemyScenarios.Load(path).Where(d => !d.Scaled).OrderBy(EnemyScenarios.Requirement).ThenBy(d => d.CombatLevel).ToList();
            Console.WriteLine($"{definitions.Count} enemies from {Path.GetFileName(path)}, {fights:N0} fights each. Swings to win with the weapon's hits");
            Console.WriteLine($"all physical, or {Share:P0} of one element. Res: the enemy's resistances (negative is a weakness).");

            foreach (var armoured in new[] { false, true })
            {
                Console.WriteLine();
                Console.WriteLine(armoured ? "Enemies in armour like the player's (the melee kit at their area's requirement):" : "Enemies as they are (no armour):");
                Console.WriteLine($"{"Enemy",-26} {"Req",4} | {"physical",8} {"fire",6} {"ice",6} {"light.",6} {"poison",6} | Res");
                Console.WriteLine(new string('-', 100));

                var random = new SeededCombatRandom(11);
                foreach (var d in definitions)
                {
                    var level = EnemyScenarios.Requirement(d);
                    var kit = gear.Melee(level);
                    var cells = new[] { Element.Physical, Element.Fire, Element.Ice, Element.Lightning, Element.Poison }
                        .Select(e => Swings(level, kit, d, e, armoured, fights, random)).ToList();
                    var r = d.Resistance;
                    var res = string.Join(" ", new[] { ("fire", r.Fire), ("ice", r.Ice), ("light", r.Lightning), ("poison", r.Poison) }
                        .Where(x => x.Item2 != 0).Select(x => x.Item1 + " " + (x.Item2 > 0 ? "+" : "") + (x.Item2 * 100).ToString("0") + "%"));
                    Console.WriteLine($"{(d.Id.Length > 26 ? d.Id.Substring(0, 26) : d.Id),-26} {level,4} | {cells[0],8} {cells[1],6} {cells[2],6} {cells[3],6} {cells[4],6} | {res}");
                }
            }
        }

        private static string Swings(int level, Loadout kit, EnemyDefinition d, Element element, bool armoured, int fights, ICombatRandom random)
        {
            double swings = 0;
            var won = 0;
            for (var i = 0; i < fights; ++i)
            {
                var player = new CombatSheet
                {
                    CombatLevel = level,
                    MaxHealth = CombatMath.MaxHealth(EnemyScenarios.HealthLevel(level)),
                    DefenseLevel = level,
                    Armor = kit.ArmorPower,
                    IsPlayer = true,
                };
                player.Melee.DamageLevel = level;
                player.Melee.AimLevel = level;
                player.Melee.Power = kit.WeaponPower;
                player.Melee.Accuracy = kit.WeaponAim;
                kit.ApplyExtras(player);
                player.Melee.Conversion = element == Element.Physical ? new ElementValues() : ElementValues.Of(element, Share);
                player.ElementBonus = new ElementValues();

                var enemy = d.Sheet();
                if (armoured) enemy.Armor = kit.ArmorPower;
                var outcome = EnemyScenarios.Fight(player.MaxHealth, enemy.MaxHealth, EnemyScenarios.Interval(d),
                    () => CombatMath.Attack(player, AttackStyle.Melee, enemy, random).Total,
                    () => CombatMath.Attack(enemy, d.Style, player, random).Total);
                if (!outcome.Won) continue;
                won++;
                swings += outcome.Swings;
            }
            return won == 0 ? "lost" : (swings / won).ToString("0.0") + (won < fights ? "*" : "");
        }
    }
}
