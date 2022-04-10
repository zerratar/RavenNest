// See https://aka.ms/new-console-template for more information


using UnityVersionChanger;

var targetFile = @"C:\git\Ravenfall Legacy\Build\Ravenfall_Data\globalgamemanagers";
var expectedVersion = "0.7.8.8a";
var replacementVersion = "0.7.8.11a";

using var gm = new BinaryFile(targetFile);

//var firstIndexOfRavenfall = gm.IndexOf("RAVENFALL", StringComparison.OrdinalIgnoreCase);
//var firstIndexOfShinobytes = gm.IndexOf("shinobytes", StringComparison.OrdinalIgnoreCase);

if (gm.IndexOf(replacementVersion, 4600) >= 0)
{
    Console.WriteLine("File has already been patched.");
    Console.ReadKey();
    return;
}

gm.Position = gm.IndexOf(expectedVersion, 4600); // we know its gonna be around 4600+, will be quicker to have that as starting point.

//gm.Position = gm.IndexOf(expectedVersion);

if (gm.Position == -1)
{
    Console.WriteLine($"Failed to update version, the expected version {expectedVersion} could not be found.");
    Console.ReadKey();
    return;
}

//var foundVersion = gm.ReadString();

gm.WriteString(replacementVersion, 25, '\0');
gm.Save();
