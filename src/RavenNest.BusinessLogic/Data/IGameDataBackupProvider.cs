using RavenNest.DataModels;
using System;

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
}
