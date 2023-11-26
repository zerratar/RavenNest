// See https://aka.ms/new-console-template for more information


using GameDataSimulation;
using RavenNest.BusinessLogic;


//var alpha = 0.01;
//Console.WriteLine("  5: " + GameMath.CalculateSuccessRate(5,  75, 0.2, 1.0, alpha));
//Console.WriteLine(" 20: " + GameMath.CalculateSuccessRate(20, 75, 0.2, 1.0, alpha));
//Console.WriteLine(" 40: " + GameMath.CalculateSuccessRate(40, 75, 0.2, 1.0, alpha));
//Console.WriteLine(" 80: " + GameMath.CalculateSuccessRate(80, 75, 0.2, 1.0, alpha));
//Console.WriteLine("100: " + GameMath.CalculateSuccessRate(100, 75, 0.2, 1.0, alpha));
//Console.WriteLine("150: " + GameMath.CalculateSuccessRate(150, 75, 0.2, 1.0, alpha));
//Console.WriteLine("200: " + GameMath.CalculateSuccessRate(200, 75, 0.2, 1.0, alpha));

//Console.ReadKey();

//using GMath = RavenNest.BusinessLogic.GameMath;

//var villages = new List<UserVillage>();
////for (var j = 0; j < 7; j++)
////{
////    var level = j == 0 ? 1 : (j * 50) - 1;
////    for (var i = 0; i < 4; i++)
////    {
////        var population = (i % 4) * 10;//100;
////        villages.Add(new UserVillage
////        {
////            Level = level,
////            Name = "Village " + j + "#" + i,
////            Population = population
////        });
////    }
////}

//for (var i = 0; i < 15; i++)
//{
//    var population = i * 10;//100;
//    villages.Add(new UserVillage
//    {
//        Level = 1,
//        Name = "Village " + 1 + "#" + i,
//        Population = population
//    });
//}

////var villages = new UserVillage[]
////{
////    new UserVillage { Level = 10, Population = 25, Name = "Someone" },
////    new UserVillage { Level = 170, Population = 352, Name = "LosCautroAmigos" },
////    new UserVillage { Level = 182, Population = 585, Name = "RavenMMO" },
////    new UserVillage { Level = 200, Population = 481, Name = "KeyPandora" },
////};
///*

//loscuatroamigos lv 170, 352, 2.83 xp/h/p
//ravenmmo lv 182, 585, 1.97xp/h/p
//KeyPandora lv 197, 481, 2.85xp/h/p

// */

//void ConsoleValue(string label, object value)
//{
//    Console.ResetColor();
//    Console.ForegroundColor = ConsoleColor.Gray;
//    Console.Write(label.PadRight(14, ' '));
//    Console.ForegroundColor = ConsoleColor.Cyan;
//    Console.WriteLine(value.ToString());
//}

//void WriteHeader(int padding, params object[] columns)
//{
//    var strValues = columns.Select(x => x.ToString()).ToArray();
//    //var padding = strValues.Max(x => x.Length);
//    var tableWidth = (padding + 2) * columns.Length + 4;

//    //╔═══════════════╦═══════╦═══════════════════════╗
//    Console.Write("╔");
//    for (var i = 0; i < columns.Length; ++i)
//    {
//        for (var j = 0; j < padding + 2; ++j)
//            Console.Write("═");
//        if (i < columns.Length - 1)
//            Console.Write("╦");
//    }
//    Console.WriteLine("╗");

//    Console.Write("║");
//    for (var i = 0; i < columns.Length; i++)
//    {
//        var value = " " + strValues[i].PadRight(padding, ' ') + " ";
//        Console.Write(value);
//        Console.Write("║");
//    }

//    Console.WriteLine();
//}

//void WriteRow(int padding, params object[] values)
//{
//    BeginRow(padding, values.Length);

//    Console.Write("║");
//    for (var i = 0; i < values.Length; i++)
//    {
//        var value = " " + values[i].ToString().PadRight(padding, ' ') + " ";
//        Console.Write(value);
//        Console.Write("║");
//    }
//    Console.WriteLine();
//}

//void BeginRow(int padding, int columns)
//{
//    Console.Write("╠");//╚
//    for (var i = 0; i < columns; ++i)
//    {
//        for (var j = 0; j < padding + 2; ++j)
//            Console.Write("═");
//        if (i < columns - 1)
//            Console.Write("╬");//╩
//    }
//    Console.WriteLine("╣");//╝
//}

//void EndTable(int padding, int columns)
//{
//    Console.Write("╚");
//    for (var i = 0; i < columns; ++i)
//    {
//        for (var j = 0; j < padding + 2; ++j)
//            Console.Write("═");
//        if (i < columns - 1)
//            Console.Write("╩");
//    }
//    Console.WriteLine("╝");
//}

//var tablePadding = 10;
//var decimalRounding = 3;
//var columns = new string[] { "Lv From", "Lv To", "Players", "XP/H", "%/H", "TTL" };
//WriteHeader(tablePadding, columns);

//foreach (var v in villages)
//{
//    var exp = GMath.GetVillageExperience(v.Level, v.Population, TimeSpan.FromSeconds(1));
//    var expNextLevel = GMath.ExperienceForLevel(v.Level + 1);
//    var percentGain = (exp / expNextLevel);

//    var expPerHour = exp * 60 * 60;
//    var percentPerHour = (percentGain * 60 * 60);

//    var secondsForLevel = expNextLevel / exp;

//    var ttl = exp == 0 ? "never" : Utility.FormatTime(TimeSpan.FromSeconds(secondsForLevel));

//    WriteRow(tablePadding, v.Level, v.Level + 1, v.Population,
//        Math.Round(expPerHour, decimalRounding),
//        Math.Round(percentPerHour * 100.0d, decimalRounding), ttl);
//    //Console.WriteLine();
//}
//EndTable(tablePadding, columns.Length);
//Console.ReadKey();

// set up potential boosts
double villageBoost = 31d;// 3139.74%
double patronBoost = 5d; // 5x
double globalExpMulti = 100d; // 100x
double rested = 2d; // 2x


// with boost
double expBoost = rested * (villageBoost + patronBoost + globalExpMulti);

//// without rested
//expBoost = (villageBoost + patronBoost + globalExpMulti);

//// without boost
//expBoost = 1;


int startLevel = 1;
int nextLevel = 1001;
var levelIncrement = 20;

void UseOldSettings()
{
    GameMath.Exp.EasyLevel = 70;
    GameMath.Exp.IncrementMins = 14;
    GameMath.Exp.EasyLevelIncrementDivider = 8;
    GameMath.Exp.GlobalMultiplierFactor = 1;
}

void UseNewSettings()
{
    GameMath.Exp.EasyLevel = 999;
    GameMath.Exp.IncrementMins = 8;
    GameMath.Exp.EasyLevelIncrementDivider = 4;
    GameMath.Exp.GlobalMultiplierFactor = 0.45;
}

var padding = 20;

Console.Write("Level".PadRight(12));

Console.Write("Old (-b)".PadRight(padding));
Console.Write("Old (+b)".PadRight(padding));

Console.Write("New (-b)".PadRight(padding));
Console.Write("New (+b)".PadRight(padding));
Console.WriteLine();
var skill = GameMath.Skill.Woodcutting;
var playersInArea = 50;
var boost = expBoost;

for (var i = startLevel; i <= nextLevel; i += levelIncrement)
{
    var level = i;
    Console.Write(level.ToString().PadRight(12));
    UseOldSettings();
    WriteTimeForLevel(level);
    UseNewSettings();
    WriteTimeForLevel(level);
    Console.WriteLine();
}


void WriteTimeForLevel(int level)
{
    // without boost
    var ticksForLevel = GameMath.Exp.GetTotalTicksForLevel(level, skill, playersInArea);
    var ticksPerSeconds = GameMath.Exp.GetTicksPerSeconds(skill, playersInArea);
    var timeLeftToLevel = TimeSpan.FromSeconds(ticksForLevel / ticksPerSeconds);
    Console.Write(timeLeftToLevel.ToString().PadRight(padding));

    // with boost
    var effectiveBoost = GameMath.Exp.GetEffectiveExpMultiplier(level, boost);
    var bTicksForLevel = GameMath.Exp.GetTotalTicksForLevel(level, skill, boost, playersInArea);
    var bTimeLeftToLevel = TimeSpan.FromSeconds(bTicksForLevel / ticksPerSeconds);
    Console.Write(bTimeLeftToLevel.ToString().PadRight(padding));
}
Console.ReadKey();
while (true)
{
    new SkillLevelingSimulation().Run(new SkillLevelingSimulationSettings
    {
        Skill = GameMath.Skill.Attack,
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
