using Ionic.Zip;
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
    public interface IGameDataBackupProvider
    {
        void CreateRestorePoint(IEntitySet[] entitySets);
        void CreateBackup(IEntitySet[] entitySets);
        void ClearRestorePoint();
        byte[] GetCompressedEntityStream(IEntitySet[] entitySets);

        IEntityRestorePoint GetRestorePoint(params Type[] types);
        IEntityRestorePoint GetRestorePoint(string path, params Type[] types);
    }

    public class GameDataBackupProvider : IGameDataBackupProvider
    {
        //#if RELEASE || Linux
        private const string RestorePointFolder = @"restorepoints";
        //#else
        //        private const string RestorePointFolder = @"C:\git\RavenNest\src\RavenNest.Blazor\restorepoints";
        //#endif
        private const string BackupFolder = "backups";
        private const string FileTypeExt = ".json";

        private readonly object ioMutex = new object();

        public GameDataBackupProvider()
        {
            if (!System.IO.Directory.Exists(RestorePointFolder))
            {
                System.IO.Directory.CreateDirectory(RestorePointFolder);
            }

            if (!System.IO.Directory.Exists(BackupFolder))
            {
                System.IO.Directory.CreateDirectory(BackupFolder);
            }
        }

        public void ClearRestorePoint()
        {
            lock (ioMutex)
            {
                if (System.IO.Directory.Exists(RestorePointFolder))
                {
                    System.IO.Directory.Delete(RestorePointFolder, true);
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
                var backupFolders = System.IO.Directory.GetDirectories(BackupFolder);
                if (backupFolders.Length > 10)
                {
                    var toDelete = backupFolders.OrderByDescending(x => new System.IO.DirectoryInfo(x).CreationTime).Skip(10);
                    foreach (var old in toDelete)
                    {
                        System.IO.Directory.Delete(old, true);
                    }
                }
            }
            catch (Exception exc)
            {
                // ignored for now
            }
        }

        public void CreateRestorePoint(IEntitySet[] entitySets)
        {
            lock (ioMutex)
            {
                if (!System.IO.Directory.Exists(RestorePointFolder))
                {
                    System.IO.Directory.CreateDirectory(RestorePointFolder);
                }
            }

            StoreData(entitySets, RestorePointFolder);
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
                var restorePointFiles = System.IO.Directory.GetFiles(RestorePointFolder, "*" + FileTypeExt);
                if (restorePointFiles.Length == 0)
                {
                    return null;
                }
            }

            var restorePoint = new EntityRestorePoint();
            try
            {
                foreach (var type in types)
                {
                    var entities = LoadEntities(type, RestorePointFolder);
                    if (entities != null && entities.Count > 0)
                        restorePoint.AddEntities(type, entities);
                }

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
            var backupFolder = System.IO.Path.Combine(BackupFolder, timeNow.Ticks.ToString());
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
            private ConcurrentDictionary<Type, IReadOnlyList<IEntity>> entityData =
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
