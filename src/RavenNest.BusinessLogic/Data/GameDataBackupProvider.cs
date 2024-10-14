using Ionic.Zip;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RavenNest.DataModels;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace RavenNest.BusinessLogic.Data
{
    public class GameDataBackupProvider
    {
        const int backupsToKeep = 10;
        //#if RELEASE || Linux
        private const string FileTypeExt = ".json";
        private readonly string FullBackupsPath = Path.Combine(FolderPaths.GeneratedData, FolderPaths.Backups);
        private readonly string FullRestorePointPath = Path.Combine(FolderPaths.GeneratedData, FolderPaths.Restorepoints);
        private readonly string FullMergePath = Path.Combine(FolderPaths.GeneratedData, FolderPaths.Merge);
        //#else
        //        private const string fullRestorePointPath = @"C:\git\RavenNest\src\RavenNest.Blazor\restorepoints";
        //#endif


        private readonly object ioMutex = new object();
        private readonly ILogger<GameDataBackupProvider> logger;

        public GameDataBackupProvider(ILogger<GameDataBackupProvider> logger)
        {
            if (!System.IO.Directory.Exists(FullRestorePointPath))
            {
                System.IO.Directory.CreateDirectory(FullRestorePointPath);
            }

            if (!System.IO.Directory.Exists(FullMergePath))
            {
                System.IO.Directory.CreateDirectory(FullMergePath);
            }

            if (!System.IO.Directory.Exists(FullBackupsPath))
            {
                System.IO.Directory.CreateDirectory(FullBackupsPath);
            }

            this.logger = logger;
        }

        public void ClearRestorePoint(IEntitySet entitySet)
        {
            lock (ioMutex)
            {
                ClearRestorePoint(entitySet.GetEntityType());
            }
        }

        public void ClearRestorePoint(Type type)
        {
            lock (ioMutex)
            {
                var restorepointFile = GetEntityFilePath(type, FullRestorePointPath);
                if (System.IO.File.Exists(restorepointFile))
                {
                    System.IO.File.Delete(restorepointFile);
                }
            }
        }

        public void ClearMerge(Type type)
        {
            lock (ioMutex)
            {
                var restorepointFile = GetEntityFilePath(type, FullMergePath);
                if (System.IO.File.Exists(restorepointFile))
                {
                    System.IO.File.Delete(restorepointFile);
                }
            }
        }

        public void ClearRestorePoint()
        {
            lock (ioMutex)
            {
                //delete files in restorepoints folder. Don't need it anymore
                var restorePointFilesToDelete = System.IO.Directory.GetFiles(FullRestorePointPath, "*" + FileTypeExt);
                if (restorePointFilesToDelete.Length > 0)
                {
                    foreach (var old in restorePointFilesToDelete)
                    {
                        System.IO.File.Delete(old);
                    }
                }
            }
        }

        public void CreateBackup(IEntitySet[] entitySets)
        {
            RemoveOldBackupFolders();
            RemoveOldBackupZipArchives();
            try
            {
                // s FullBackupsPath
                var path = GetBackupZipPath();
                using (var fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
                {
                    WriteCompressedEntitiesToStream(fs, entitySets);
                    return;
                }
            }
            catch
            {
                var backupFolder = GetBackupFolder();
                StoreData(entitySets, backupFolder);
            }
        }

        private void RemoveOldBackupZipArchives()
        {
            try
            {
                var backupFiles = Directory.GetFiles(FullBackupsPath, "*.zip")
                    .Select(x => new FileInfo(x))
                    .OrderByDescending(x => x.CreationTime)
                    .ToList();

                var dateNow = DateTime.Now.Date;
                var keepToday = 5;

                var toDelete = new List<FileInfo>();

                // Group by the date of creation
                var groupedByDate = backupFiles.GroupBy(x => x.CreationTime.Date);

                foreach (var group in groupedByDate)
                {
                    if (group.Key == dateNow)
                    {
                        // For today's files, keep the first, last, and three in between.
                        var toKeep = group.Take(1)
                            .Concat(group.Skip(1).Take(group.Count() - 2).OrderBy(y => Guid.NewGuid()).Take(keepToday - 2))
                            .Concat(group.Skip(group.Count() - 1).Take(1))
                            .ToList();

                        toDelete.AddRange(group.Except(toKeep));
                    }
                    else
                    {
                        // For other days, keep the oldest and newest files, and delete the rest.
                        var toKeepForOtherDays = group.OrderByDescending(x => x.CreationTime).Take(1).Concat(group.OrderBy(x => x.CreationTime).Take(1)).ToList();

                        toDelete.AddRange(group.Except(toKeepForOtherDays));
                    }
                }

                // Delete the files marked for deletion
                foreach (var fileInfo in toDelete)
                {
                    fileInfo.Delete();
                }
            }
            catch (Exception exc)
            {
                logger.LogError("Error removing old archive backups: " + exc);
            }
        }

        private void CreateBackup(EntityRestorePoint restorePoint)
        {
            var backupFolder = GetBackupFolder();
            IReadOnlyList<Type> types = restorePoint.GetEntityTypes();
            foreach (var type in types)
            {
                var entities = restorePoint.Get(type);
                StoreEntities(type, entities, backupFolder);
            }

            RemoveOldBackupFolders();
        }

        private void RemoveOldBackupFolders()
        {
            try
            {
                var backupFolders = Directory.GetDirectories(FullBackupsPath);

                var backupsDay = backupFolders.Select(x => new DirectoryInfo(x)).GroupBy(x => x.CreationTime.Date).ToList();
                foreach (var b in backupsDay)
                {
                    // if its today, keep backupsToKeep
                    // otherwise, only keep 1.
                    var skip = 1;
                    if (b.Key.Date == DateTime.Today.Date)
                    {
                        skip = backupsToKeep;
                    }

                    var list = b.ToList();
                    if (list.Count > backupsToKeep)
                    {
                        var toDelete = list.OrderByDescending(x => x.CreationTime).Skip(skip);
                        foreach (var old in toDelete)
                        {
                            old.Delete(true);
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                logger.LogError("Error removing old backups: " + exc);
            }
        }

        public void CreateRestorePoint(IEntitySet[] entitySets)
        {
            lock (ioMutex)
            {
                if (!System.IO.Directory.Exists(FullRestorePointPath))
                {
                    System.IO.Directory.CreateDirectory(FullRestorePointPath);
                }
            }

            StoreData(entitySets, FullRestorePointPath);
        }

        public void WriteCompressedEntitiesToStream(Stream stream, IEntitySet[] entitySets)
        {
            using (ZipFile zip = new ZipFile())
            {
                foreach (var entitySet in entitySets)
                {
                    var type = entitySet.GetEntityType();
                    var entities = entitySet.GetEntities();
                    var key = type.FullName + ".json";
                    var data = JsonConvert.SerializeObject(entities);
                    zip.AddEntry(key, data);
                }

                zip.Save(stream);
            }
        }

        private void StoreData(IEntitySet[] entitySets, string dataFolder)
        {
            foreach (var entitySet in entitySets)
            {
                var type = entitySet.GetEntityType();
                var entities = entitySet.GetEntities();
                StoreEntities(type, entities, dataFolder);
            }
        }
        private void StoreEntities(Type type, IReadOnlyList<IEntity> entities, string dataFolder)
        {
            lock (ioMutex)
            {
                var file = GetEntityFilePath(type, dataFolder);
                var data = JsonConvert.SerializeObject(entities);
                System.IO.File.WriteAllText(file, data);
            }
        }

        public IEntityRestorePoint GetRestorePoint(string path, params Type[] types)
        {
            lock (ioMutex)
            {
                var restorePointFiles = System.IO.Directory.GetFiles(path, "*" + FileTypeExt);
                if (restorePointFiles.Length == 0)
                {
                    return null;
                }
            }

            var restorePoint = new EntityRestorePoint();
            foreach (var type in types)
            {
                var entities = LoadEntities(type, path);
                if (entities != null && entities.Count > 0)
                    restorePoint.AddEntities(type, entities);
            }

            return restorePoint;
        }

        public IEntityRestorePoint GetMergeData(params Type[] types)
        {
            var path = FullMergePath;
            return GetRestorePoint(path, false, types);
        }

        public IEntityRestorePoint GetRestorePoint(params Type[] types)
        {
            var path = FullRestorePointPath;
            return GetRestorePoint(path, true, types);
        }

        public IEntityRestorePoint GetRestorePoint(string path, bool createBackup, params Type[] types)
        {
            lock (ioMutex)
            {
                // check if restore point is provided using zip file, if so, we want to unpack it. delete the zip file and then start the restore.
                //var zipFile = Directory.GetFiles(FullRestorePointPath, "*.zip").FirstOrDefault();
                //if (zipFile != null)
                //{
                //    SharpCompress
                //}

                var restorePointFiles = Directory.GetFiles(path, "*" + FileTypeExt);
                if (restorePointFiles.Length == 0)
                {
                    logger?.LogInformation("No restore point available. Skipping");
                    return null;
                }
            }

            var restorePoint = new EntityRestorePoint();
            try
            {
                var entitiesToRestore = new List<string>();
                foreach (var type in types)
                {
                    var entities = LoadEntities(type, path);
                    if (entities != null && entities.Count > 0)
                    {
                        restorePoint.AddEntities(type, entities);
                        entitiesToRestore.Add(entities.Count + " " + type.Name);
                    }
                }

                logger?.LogError("Restore point found, data will be restored to the files found: " + string.Join(", ", entitiesToRestore));
                return restorePoint;
            }
            finally
            {
                if (createBackup)
                    CreateBackup(restorePoint);
            }
        }


        private string GetBackupZipPath()
        {
            var timeNow = DateTime.UtcNow;
            return Path.Combine(FullBackupsPath, timeNow.Ticks.ToString() + ".zip");
        }

        private string GetBackupFolder()
        {
            var timeNow = DateTime.UtcNow;
            var backupFolder = System.IO.Path.Combine(FullBackupsPath, timeNow.Ticks.ToString());
            lock (ioMutex)
            {
                if (!System.IO.Directory.Exists(backupFolder))
                    System.IO.Directory.CreateDirectory(backupFolder);
            }

            return backupFolder;
        }



        private IReadOnlyList<IEntity> LoadEntities(Type type, string repositoryFolder)
        {
            lock (ioMutex)
            {
                var file = GetEntityFilePath(type, repositoryFolder);
                if (!System.IO.File.Exists(file))
                    return null;

                var output = new List<IEntity>();
                var data = System.IO.File.ReadAllText(file);

                var collectionType = typeof(List<>).MakeGenericType(type);
                var entities = JsonConvert.DeserializeObject(data, collectionType) as IEnumerable;
                foreach (var obj in entities)
                {
                    if (obj is IEntity entity)
                        output.Add(entity);
                }

                return output;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string GetEntityFilePath(Type type, string dataFolder)
        {
            return System.IO.Path.Combine(dataFolder, type.FullName + FileTypeExt);
        }

        private class EntityRestorePoint : IEntityRestorePoint
        {
            private readonly ConcurrentDictionary<Type, IReadOnlyList<IEntity>> entityData =
                new ConcurrentDictionary<Type, IReadOnlyList<IEntity>>();

            public IReadOnlyList<T> Get<T>() where T : IEntity
            {
                try
                {
                    var entities = GetEntities(typeof(T));
                    if (entities == null) return null;
                    var casted = entities.Cast<T>();
                    return casted?.ToList();
                }
                catch { return null; }
            }

            public IReadOnlyList<IEntity> Get(Type type)
            {
                return GetEntities(type);
            }

            private IReadOnlyList<IEntity> GetEntities(Type type)
            {
                entityData.TryGetValue(type, out var entities);
                return entities;
            }

            internal void AddEntities(Type type, IReadOnlyList<IEntity> entities)
            {
                entityData[type] = entities;
            }

            public IReadOnlyList<Type> GetEntityTypes()
            {
                return entityData.Keys.AsList();
            }
        }
    }
}
