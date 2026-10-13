-- Village house placement (build mode, phase 2).
--
-- Adds where a house has been moved to. Null in all three means the house is at its original
-- plot position in the game scene, which is every existing house, so no data changes.
--
-- Run this by hand before the server build that adds PositionX, PositionZ and Rotation to
-- VillageHouse ships. This project has no EF migrations, every save writes every property, and
-- GameData loads every entity set at boot, so the columns must exist first.
--
-- Safe to run twice.

-- Since DatabaseSchema (src/RavenNest.BusinessLogic/Data/DatabaseSchema.cs), the server adds missing
-- columns itself at startup, so running this by hand is no longer required. It is kept as a record of
-- the change and for adding the columns ahead of a deploy.
--

SET NOCOUNT ON;

IF COL_LENGTH(N'dbo.VillageHouse', N'PositionX') IS NULL
BEGIN
    ALTER TABLE dbo.VillageHouse ADD PositionX float NULL;
    PRINT 'Added dbo.VillageHouse.PositionX';
END

IF COL_LENGTH(N'dbo.VillageHouse', N'PositionZ') IS NULL
BEGIN
    ALTER TABLE dbo.VillageHouse ADD PositionZ float NULL;
    PRINT 'Added dbo.VillageHouse.PositionZ';
END

IF COL_LENGTH(N'dbo.VillageHouse', N'Rotation') IS NULL
BEGIN
    ALTER TABLE dbo.VillageHouse ADD Rotation float NULL;
    PRINT 'Added dbo.VillageHouse.Rotation';
END
