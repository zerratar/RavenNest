// See https://aka.ms/new-console-template for more information


using GameDataSimulation;
using GMath = RavenNest.BusinessLogic.GameMath;


var playerCount = 300;
var villageLevel = 170;
var exp = GMath.GetVillageExperience(villageLevel, playerCount, TimeSpan.FromSeconds(1));
Console.WriteLine("Enchanting exp gained from a Abraxas 2h Sword (Max Exp)");

var expNextLevel = GMath.ExperienceForLevel(villageLevel + 1);
var percentGain = exp / expNextLevel * 100.0;

Console.WriteLine("Skill Level: " + villageLevel);
Console.WriteLine("Item Level: " + playerCount);
Console.WriteLine("Attribute Count: " + GMath.GetMaxEnchantingAttributeCount(playerCount));
Console.WriteLine("Exp: " + exp);
Console.WriteLine("Gained %: " + percentGain);
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
