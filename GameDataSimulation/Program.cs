// See https://aka.ms/new-console-template for more information


using GameDataSimulation;
using RavenNest.BusinessLogic.Game;
using System.Drawing.Text;
using GMath = RavenNest.BusinessLogic.GameMath;

var villages = new List<UserVillage>();
for (var j = 0; j < 5; j++)
{
    var level = 1 + (j * 50);
    for (var i = 0; i < 4; i++)
    {
        var population = (i % 4) * 100;
        villages.Add(new UserVillage
        {
            Level = level,
            Name = "Village " + j + "#" + i,
            Population = population
        });
    }
}
//var villages = new UserVillage[]
//{
//    new UserVillage { Level = 10, Population = 25, Name = "Someone" },
//    new UserVillage { Level = 170, Population = 352, Name = "LosCautroAmigos" },
//    new UserVillage { Level = 182, Population = 585, Name = "RavenMMO" },
//    new UserVillage { Level = 200, Population = 481, Name = "KeyPandora" },
//};
/*

loscuatroamigos lv 170, 352, 2.83 xp/h/p
ravenmmo lv 182, 585, 1.97xp/h/p
KeyPandora lv 197, 481, 2.85xp/h/p

 */

void ConsoleValue(string label, object value)
{
    Console.ResetColor();
    Console.ForegroundColor = ConsoleColor.Gray;
    Console.Write(label.PadRight(14, ' '));
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine(value.ToString());
}

foreach (var v in villages)
{
    var exp = GMath.GetVillageExperience(v.Level, v.Population, TimeSpan.FromSeconds(1));
    var expNextLevel = GMath.ExperienceForLevel(v.Level + 1);
    var percentGain = (exp / expNextLevel);

    var expPerHour = exp * 60 * 60;
    var percentPerHour = (percentGain * 60 * 60);

    var secondsForLevel = expNextLevel / exp;
    var ttl = TimeSpan.FromSeconds(secondsForLevel);

    ConsoleValue("Lv:", v.Level);
    ConsoleValue("Players:", v.Population);
    ConsoleValue("XP/H and %/H:", expPerHour + "\t" + (percentPerHour * 100.0));
    if (v.Population > 0)
        ConsoleValue("XP/P/H:", (expPerHour / v.Population));
    ConsoleValue("Time To Lv:", Utility.FormatTime(ttl));
    Console.WriteLine();
}

Console.ReadKey();

double expBoost = 250;
int nextLevel = 999;
var levelIncrement = 1;
while (true)
{
    new SkillLevelingSimulation().Run(new SkillLevelingSimulationSettings
    {
        Skill = Skill.Attack,
        ExpBoost = expBoost,
        NextLevel = nextLevel,
        MultiplierFactor = 1.0,
        ExpFactor = 1.0,
        PlayersInArea = 100,
        //Exp = 6000,
        //ExpGainType = ExpGainType.Fixed,
    });

    // Home, Away, Ironhill, Kyo, Heim

    Console.WriteLine();
    Console.Write(" > Next Level (Default " + (nextLevel + levelIncrement) + ", add + in end for boost): ");

    var lv = Console.ReadLine() ?? "";
    var checkBoost = false;
    if (lv.EndsWith("+"))
    {
        lv = lv.Replace("+", "");
        checkBoost = true;
    }

    var oldLv = nextLevel;
    if (!int.TryParse(lv, out nextLevel))
    {
        nextLevel = oldLv + levelIncrement;
    }

    if (checkBoost)
    {
        Console.Write(" > Exp Boost (Default 1): ");
        if (!double.TryParse(Console.ReadLine(), out expBoost))
            expBoost = 1;
    }

    Console.Clear();
}

//new MiningDropSimulation().Run(new MiningDropSimulationSettings
//{
//    MiningLevel = 332,
//    SimulateGamePlayHours = 6,
//    TimeScaleFactor = 1000
//});

while (true) { System.Threading.Thread.Sleep(1000); }



struct UserVillage
{
    public string Name;
    public int Level;
    public int Population;
}
