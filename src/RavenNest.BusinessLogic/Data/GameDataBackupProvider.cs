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

            if (!System.IO.Directory.Exists(FullBackupsPath))
            {
                System.IO.Directory.CreateDirectory(FullBackupsPath);
            }

            this.logger = logger;
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
            RemoveOldBackups();
            var backupFolder = GetBackupFolder();
            StoreData(entitySets, backupFolder);
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
            RemoveOldBackups();
        }

        private void RemoveOldBackups()
        {
            try
            {
                var backupFolders = System.IO.Directory.GetDirectories(FullBackupsPath);
                var backupsDay = backupFolders.Select(x => new System.IO.DirectoryInfo(x)).GroupBy(x => x.CreationTime.Date).ToList();
                foreach (var b in backupsDay)
                {
                    // if its today, keep backupsToKeep
                    // otherwise, only keep 1.
                    var skip= 1;
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
            catch
            {
                // ignored for now
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

        public byte[] GetCompressedEntityStream(IEntitySet[] entitySets)
        {
            using (var memoryStream = new MemoryStream())
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

                zip.Save(memoryStream);
                return memoryStream.ToArray();
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

        public IEntityRestorePoint GetRestorePoint(params Type[] types)
        {
            lock (ioMutex)
            {
                // check if restore point is provided using zip file, if so, we want to unpack it. delete the zip file and then start the restore.
                //var zipFile = Directory.GetFiles(FullRestorePointPath, "*.zip").FirstOrDefault();
                //if (zipFile != null)
                //{
                //    SharpCompress
                //}

                var restorePointFiles = Directory.GetFiles(FullRestorePointPath, "*" + FileTypeExt);
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
                    var entities = LoadEntities(type, FullRestorePointPath);
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
                CreateBackup(restorePoint);
            }
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
