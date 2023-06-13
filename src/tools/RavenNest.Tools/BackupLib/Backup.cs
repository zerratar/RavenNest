using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace RavenNest.Tools.BackupLib
{
    public class Backup<T, T2, T3> : Backup<T, T2>
    {
        public List<T3> Data2 { get; set; }
    }

    public class Backup<T, T2> : Backup<T>
    {
        public List<T2> Data1 { get; set; }
    }

    public class Backup<T> : Backup
    {
        public List<T> Data0 { get; set; }
    }

    public abstract class Backup
    {

        public System.DateTime Created { get; set; }
    }

    public class BackupInfo
    {
        public string Folder { get; set; }
        public DateTime Created { get; set; }
    }

    public static class Backups
    {
        public static List<CharacterSkillBackup> GetSkillBackups(string inputFolder, Action<int, int> onLoadProgressed)
        {
            var backup = new List<CharacterSkillBackup>();
            var items = Get<DataModels.Character, DataModels.Skills>(inputFolder, onLoadProgressed);
            foreach (var item in items)
            {
                backup.Add(new CharacterSkillBackup(item));
            }
            return backup;
        }

        public static List<DataModels.InventoryItem> GetInventoryItems(string inputFolder)
        {
            return List<DataModels.InventoryItem>(inputFolder);
        }

        public static List<BackupInfo> GetBackups(string inputFolder)
        {
            var result = new List<BackupInfo>();
            var backupFolders = System.IO.Directory.GetDirectories(inputFolder);
            foreach (var bf in backupFolders)
            {
                var name = new DirectoryInfo(bf).Name;
                var data = name.Split('_');
                var str = data[1].AsSpan();
                var date = DateTime.Parse(data[0] + " " + string.Join(":", str[..2].ToString(), str.Slice(2, 2).ToString(), str.Slice(4, 2).ToString()));
                result.Add(new BackupInfo
                {
                    Created = date,
                    Folder = bf
                });
            }
            return result;
        }

        public static List<Backup<T1, T2>> Get<T1, T2>(string inputFolder, Action<int, int> onLoadProgressed)
        {
            var result = new List<Backup<T1, T2>>();

            // Map each backup folder with a datetime stamp
            // Then have a state of the data loaded, based on which data we need.            
            var backupFolders = System.IO.Directory.GetDirectories(inputFolder);

            var i = 0;
            onLoadProgressed(i++, backupFolders.Length);
            foreach (var bf in backupFolders)
            {
                var name = new DirectoryInfo(bf).Name;
                var data = name.Split('_');
                var str = data[1].AsSpan();
                var date = DateTime.Parse(data[0] + " " + string.Join(":", str[..2].ToString(), str.Slice(2, 2).ToString(), str.Slice(4, 2).ToString()));

                var backup = new Backup<T1, T2>();
                backup.Created = date;
                backup.Data0 = List<T1>(bf);
                backup.Data1 = List<T2>(bf);
                result.Add(backup);
                onLoadProgressed(i++, backupFolders.Length);
            }
            return result;
        }

        public static List<Backup<T1, T2, T3>> Get<T1, T2, T3>(string inputFolder)
        {
            var result = new List<Backup<T1, T2, T3>>();

            // Map each backup folder with a datetime stamp
            // Then have a state of the data loaded, based on which data we need.
            var backupFolders = System.IO.Directory.GetDirectories(inputFolder);
            foreach (var bf in backupFolders)
            {
                var name = new DirectoryInfo(bf).Name;
                var data = name.Split('_');
                var str = data[1].AsSpan();
                var date = DateTime.Parse(data[0] + " " + string.Join(":", str[..2].ToString(), str.Slice(2, 2).ToString(), str.Slice(4, 2).ToString()));

                var backup = new Backup<T1, T2, T3>();
                backup.Created = date;
                backup.Data0 = List<T1>(bf);
                backup.Data1 = List<T2>(bf);
                backup.Data2 = List<T3>(bf);
                result.Add(backup);
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static List<T> List<T>(string backupFolder)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<List<T>>(Read<T>(backupFolder));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string Read<T>(string backupFolder)
        {
            return System.IO.File.ReadAllText(System.IO.Path.Combine(backupFolder, BackupUtilities.File<T>()));
        }
    }


    public class CharacterSkillBackup
    {
        public CharacterSkillBackup(Backup<DataModels.Character, DataModels.Skills> source)
        {
            this.Created = source.Created;
            this.Characters = source.Data0.ToDictionary(x => x.Id, x => x);
            this.Skills = source.Data1.ToDictionary(x => x.Id, x => x);
        }

        public CharacterSkillBackup(CharacterSkillBackup source)
        {
            this.Created = source.Created;
            this.Characters = source.Characters.ToDictionary(x => x.Key, x => x.Value); // make a copy
            this.Skills = source.Skills.ToDictionary(x => x.Key, x => x.Value); // make a copy
        }

        public CharacterSkillBackup(DateTime created)
        {
            this.Created = created;
            this.Characters = new Dictionary<Guid, DataModels.Character>();
            this.Skills = new Dictionary<Guid, DataModels.Skills>();
        }

        public DateTime Created { get; private set; }
        public Dictionary<Guid, DataModels.Character> Characters { get; private set; }
        public Dictionary<Guid, DataModels.Skills> Skills { get; private set; }
    }

    public class SplitBackup<T, T2>
    {
        public Backup<T, T2> Affected { get; set; }
        public Backup<T, T2> Unaffected { get; set; }
    }

    public static class BackupUtilities
    {

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string File(string typeName)
        {
            return typeName + ".json";
        }

        // just a quicker way to access same function. as you can do File(item)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string File<T>(T data)
        {
            return File(NameOf<T>());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string File<T>()
        {
            return File(NameOf<T>());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string NameOf<T>()
        {
            return NameOf(typeof(T));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string NameOf(Type t)
        {
            if (t.GenericTypeArguments != null && t.GenericTypeArguments.Length > 0)
            {
                return t.GenericTypeArguments[0].FullName;
            }

            if (t.IsArray && t.HasElementType)
            {
                return t.GetElementType().FullName;
            }

            return t.FullName;
        }
    }

    public static class RestorepointUtilities
    {

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Out(string restoreFolder, string typeName)
        {
            return System.IO.Path.Combine(restoreFolder, BackupUtilities.File(typeName));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Write<T>(string restoreFolder, T data)
        {
            System.IO.File.WriteAllText(Out(restoreFolder, BackupUtilities.NameOf<T>()), Newtonsoft.Json.JsonConvert.SerializeObject(data));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static List<T> Read<T>(string restoreFolder)
        {
            var file = Out(restoreFolder, BackupUtilities.NameOf<T>());
            return Newtonsoft.Json.JsonConvert.DeserializeObject<List<T>>(System.IO.File.ReadAllText(file));
        }
    }
}
