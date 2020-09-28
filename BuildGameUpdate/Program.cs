using SharpCompress.Archives.SevenZip;
using System;
using System.IO;

namespace BuildGameUpdate
{
    class Program
    {
        static void Main(string[] args)
        {
            var version = "0.5.5a";
            var fileDirectory = @"C:\git\Ravenfall-Legacy\Build";
            var files = System.IO.Directory.GetFiles(fileDirectory);
            //string[] fullPackageFiles = GetFullPackageFiles(files);
            //string[] updatePackageFiles = GetUpdatePackageFiles(files);
            //Create7ZipPackage("Ravenfall.v" + version + "-alpha.7z", fileDirectory, fullPackageFiles);
            //Create7ZipPackage("update.7z", fileDirectory, updatePackageFiles);
        }

        //private static void Create7ZipPackage(string name, string fileDirectory, string[] fullPackageFiles)
        //{
        //    var file = System.IO.Path.Combine(fileDirectory, name);
        //    using (FileStream sevenZipFile = File.Open(file, FileMode.Create))
        //    {
        //        using (var archive = SevenZipArchive.Open(sevenZipFile))
        //        {
        //            archive.CreateEntry("data.bin", "file.dat");
        //            archive.Save(sevenZipFile);
        //        }
        //    }
        //}

        //private static string[] GetUpdatePackageFiles(string[] files)
        //{
        //}

        //private static string[] GetFullPackageFiles(string[] files)
        //{
        //}
    }
}
