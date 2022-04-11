// See https://aka.ms/new-console-template for more information


using UnityVersionChanger;

var targetFile = @"C:\git\Ravenfall Legacy\Build\Ravenfall_Data\globalgamemanagers";
//var targetFile = @"C:\Users\kaaru\Downloads\Ravenfall.v0.7.8.10a-alpha\Ravenfall_Data\globalgamemanagers";

var expectedVersion = "0.7.8.8a";
//var expectedVersion = "0.7.8.10a";
var replacementVersion = "0.7.8.11a";

using var gm = new BinaryFile(targetFile);

const int versionLengthPos = 4632;    // 4 bytes later, that means size is of an int, but not only that. Many different versions have shown the same position. Header fixed size? Unity Version Related?
const int versionStringIndex = 4636;  // 

//var firstIndexOfRavenfall = gm.IndexOf("RAVENFALL", StringComparison.OrdinalIgnoreCase);
//var firstIndexOfShinobytes = gm.IndexOf("shinobytes", StringComparison.OrdinalIgnoreCase);

// Somewhere in the file we have a length of the current version string.
// even if it isnt just before, lets see if we can find it.
// expeected length is 9 bytes.,
// lets take the version position and go backwards as we will most likely find it before the string being read.

var versionLength = gm.ReadByte(versionLengthPos);
if (versionLength != expectedVersion.Length)
{
    var actualVersion = gm.ReadString(versionStringIndex);
    if (actualVersion == replacementVersion)
    {
        Console.WriteLine("File has already been patched.");
        Console.ReadKey();
        return;
    }
    Console.WriteLine($"File is not of the expected version {expectedVersion}. It is version {actualVersion}. Abort patch");
    Console.ReadKey();
    return;
}

//Use the following to find the index of the version length byte
//var index = 0;
////var indices = new List<int>();
//for (var i = 0; i < 100; ++i)
//{
//    index = gm.IndexOf((byte)expectedVersion.Length, index);
//    //indices.Add(index);
//    if (index >= 4600) break;
//    if (index == -1) break;
//    index++;
//}

if (gm.IndexOf(replacementVersion, 4600) >= 0)
{
    Console.WriteLine("File has already been patched.");
    Console.ReadKey();
    return;
}

gm.Position = gm.IndexOf(expectedVersion, 4600); // we know its gonna be around 4600+, will be quicker to have that as starting point.

if (gm.Position == -1)
{
    Console.WriteLine($"Failed to update version, the expected version {expectedVersion} could not be found.");
    Console.ReadKey();
    return;
}

//var foundVersion = gm.ReadString();
gm.WriteByte((byte)replacementVersion.Length, 4632);
gm.WriteString(replacementVersion, 25, '\0');
gm.Save(true);
