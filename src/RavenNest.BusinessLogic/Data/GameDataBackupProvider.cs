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
        private readonly string FullBackupsPath = Path.Combine(FolderPaths.GeneratedDataPath, FolderPaths.Backups);
        private readonly string FullRestorePointPath = Path.Combine(FolderPaths.GeneratedDataPath, FolderPaths.Restorepoints);
        private readonly string FullMergePath = Path.Combine(FolderPaths.GeneratedDataPath, FolderPaths.Merge);
        private readonly string FullPendingChangesPath = Path.Combine(FolderPaths.GeneratedDataPath, FolderPaths.PendingChanges);
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

            if (!System.IO.Directory.Exists(FullPendingChangesPath))
            {
                System.IO.Directory.CreateDirectory(FullPendingChangesPath);
            }

            this.logger = logger;
        }

        #region Pending changes
        // A set's unsaved changes (adds, updates and deletes) are kept on disk while some of them keep failing to
        // save, and put back as unsaved changes when the server starts: the database is loaded as it is and only the
        // difference is written, by the usual save. They replace the old restore points, a copy of the whole table
        // that was put back by emptying the table and inserting every row, and left the table empty when one row
        // could not be inserted (2026-11-05: a boat look too long for InventoryItem.Tag stopped the server).

        /// <summary>Writes the set's unsaved changes, replacing what was written for it before.</summary>
        public void WritePendingChanges(IEntitySet entitySet)
        {
            var file = GetEntityFilePath(entitySet.GetEntityType(), FullPendingChangesPath);
            var data = JsonConvert.SerializeObject(new
            {
                Written = DateTime.UtcNow,
                Added = entitySet.Added.Select(x => x.Entity).Where(x => x != null).ToList(),
                Updated = entitySet.Updated.Select(x => x.Entity).Where(x => x != null).ToList(),
                Removed = entitySet.Removed.Select(x => x.Entity?.Id ?? Guid.Empty).Where(x => x != Guid.Empty).ToList(),
            });

            lock (ioMutex)
            {
                // written aside and moved over, so a crash while writing never leaves half a file
                var temp = file + ".tmp";
                System.IO.File.WriteAllText(temp, data);
                System.IO.File.Move(temp, file, true);
            }
        }

        /// <summary>Removes the set's pending changes file, once nothing of it waits to be saved.</summary>
        public void ClearPendingChanges(IEntitySet entitySet)
        {
            var file = GetEntityFilePath(entitySet.GetEntityType(), FullPendingChangesPath);
            lock (ioMutex)
            {
                if (System.IO.File.Exists(file))
                {
                    System.IO.File.Delete(file);
                }
            }
        }

        /// <summary>
        /// Puts the set's pending changes back, and an old style restore point of the set as the difference from
        /// what was loaded, as unsaved changes for the next save. Nothing is written to the database here. Throws
        /// when an old restore point looks incomplete, without changing anything.
        /// </summary>
        public string RestorePendingChanges(IEntitySet entitySet)
        {
            var type = entitySet.GetEntityType();
            var method = typeof(GameDataBackupProvider).GetMethod(nameof(RestorePendingChangesOf), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            try
            {
                return (string)method.MakeGenericMethod(type).Invoke(this, new object[] { entitySet });
            }
            catch (System.Reflection.TargetInvocationException exc) when (exc.InnerException != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exc.InnerException).Throw();
                throw;
            }
        }

        private string RestorePendingChangesOf<T>(EntitySet<T> set) where T : Entity<T>
        {
            var pendingFile = GetEntityFilePath(typeof(T), FullPendingChangesPath);
            var legacyFile = GetEntityFilePath(typeof(T), FullRestorePointPath);
            var report = new List<string>();

            if (System.IO.File.Exists(legacyFile))
            {
                // the backups folder keeps a copy of it
                lock (ioMutex)
                {
                    System.IO.File.Copy(legacyFile, Path.Combine(GetBackupFolder(), Path.GetFileName(legacyFile)), true);
                }

                report.Add(RestoreLegacySnapshot(set, legacyFile));
            }

            if (System.IO.File.Exists(pendingFile))
            {
                PendingChangesFile<T> pending;
                lock (ioMutex)
                {
                    pending = JsonConvert.DeserializeObject<PendingChangesFile<T>>(System.IO.File.ReadAllText(pendingFile));
                }

                var added = 0; var updated = 0; var removed = 0;
                foreach (var entity in (pending?.Added ?? new List<T>()).Concat(pending?.Updated ?? new List<T>()))
                {
                    if (entity == null) continue;
                    if (set.TryGet(entity.Id, out var existing))
                    {
                        if (CopyValues(entity, existing)) updated++;
                    }
                    else if (set.Add(entity) == AddEntityResult.Success)
                    {
                        added++;
                    }
                }

                foreach (var id in pending?.Removed ?? new List<Guid>())
                {
                    if (set.TryGet(id, out var existing) && set.Remove(existing) == RemoveEntityResult.Success)
                    {
                        removed++;
                    }
                }

                report.Add($"pending changes from {pending?.Written:u}: {added} added, {updated} updated, {removed} removed");
            }

            if (report.Count == 0)
            {
                return null;
            }

            // kept on disk, as the difference, until the save has written it; nothing left to write, nothing kept
            if (set.Added.Count + set.Updated.Count + set.Removed.Count > 0)
            {
                WritePendingChanges(set);
            }
            else
            {
                ClearPendingChanges(set);
            }

            if (System.IO.File.Exists(legacyFile))
            {
                ClearRestorePoint(typeof(T));
            }

            return typeof(T).Name + ": " + string.Join("; ", report);
        }

        /// <summary>An old restore point (the whole table) as the difference from the loaded set.</summary>
        private string RestoreLegacySnapshot<T>(EntitySet<T> set, string file) where T : Entity<T>
        {
            var snapshot = LoadEntities(typeof(T), FullRestorePointPath)?.OfType<T>().ToList() ?? new List<T>();
            var loaded = set.Entities;

            // a partly written or damaged copy would delete every row it lacks: refuse it, and change nothing
            if (snapshot.Count < loaded.Count * 0.9)
            {
                throw new DataMigrationException($"The restore point for {typeof(T).Name} has {snapshot.Count} rows and the database {loaded.Count}; " +
                    $"it looks incomplete, so nothing was restored. Check {file} and remove it to start without it.");
            }

            var inSnapshot = new HashSet<Guid>(snapshot.Select(x => x.Id));
            var added = 0; var updated = 0; var removed = 0;
            foreach (var entity in snapshot)
            {
                if (set.TryGet(entity.Id, out var existing))
                {
                    if (CopyValues(entity, existing)) updated++;
                }
                else if (set.Add(entity) == AddEntityResult.Success)
                {
                    added++;
                }
            }

            foreach (var entity in loaded)
            {
                if (!inSnapshot.Contains(entity.Id) && set.Remove(entity) == RemoveEntityResult.Success)
                {
                    removed++;
                }
            }

            return $"old restore point of {snapshot.Count} rows: {added} added, {updated} updated, {removed} removed";
        }

        /// <summary>Copies the restored values onto the loaded entity; true when any differed (it is then an unsaved update).</summary>
        private static bool CopyValues<T>(T from, T to) where T : Entity<T>
        {
            var changed = false;
            foreach (var property in CopiedProperties<T>.All)
            {
                var value = property.GetValue(from);
                if (Equals(property.GetValue(to), value)) continue;
                property.SetValue(to, value);
                changed = true;
            }

            return changed;
        }

        private static class CopiedProperties<T>
        {
            // what a restore point file holds of an entity: its public read/write properties but the id
            public static readonly System.Reflection.PropertyInfo[] All = typeof(T)
                .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .Where(x => x.CanRead && x.CanWrite && x.GetIndexParameters().Length == 0 && x.Name != nameof(IEntity.Id))
                .Where(x => x.GetCustomAttributes(typeof(JsonIgnoreAttribute), true).Length == 0)
                .ToArray();
        }

        private class PendingChangesFile<T>
        {
            public DateTime Written { get; set; }
            public List<T> Added { get; set; }
            public List<T> Updated { get; set; }
            public List<Guid> Removed { get; set; }
        }
        #endregion

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

                logger?.LogWarning("Restore point found, data will be restored to the files found: " + string.Join(", ", entitiesToRestore));
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
