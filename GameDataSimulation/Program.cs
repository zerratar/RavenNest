// See https://aka.ms/new-console-template for more information


using GameDataSimulation;
using GMath = RavenNest.BusinessLogic.GameMath;


var villages = new UserVillage[]
{
    new UserVillage { Level = 170, Population = 352, Name = "LosCautroAmigos" },
    new UserVillage { Level = 182, Population = 585, Name = "RavenMMO" },
    new UserVillage { Level = 197, Population = 481, Name = "KeyPandora" },
};

/*

loscuatroamigos lv 170, 352, 2.83 xp/h/p
ravenmmo lv 182, 585, 1.97xp/h/p
KeyPandora lv 197, 481, 2.85xp/h/p

 */

foreach (var v in villages)
{
    var exp = GMath.GetVillageExperience(v.Level, v.Population, TimeSpan.FromSeconds(1));
    var expNextLevel = GMath.ExperienceForLevel(v.Level + 1);
    var percentGain = (exp / expNextLevel);

    var expPerHour = exp * 60 * 60;

    Console.WriteLine("Name: " + v.Name);
    Console.WriteLine("Lv: " + v.Level);
    Console.WriteLine("Players: " + v.Population);
    Console.Write("Exp Per Hour: " + expPerHour);
    Console.WriteLine(" Per Player: " + (expPerHour / v.Population));
    Console.WriteLine("% Per Hour: " + (percentGain * 60 * 60) * 100.0);
    Console.WriteLine("Exp Requried: " + expNextLevel);
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
