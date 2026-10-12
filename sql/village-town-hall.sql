-- Movable town hall (build mode, town hall step A).
--
-- Adds where the streamer moved the village's town hall. Null in all three means the hall stands on
-- the plaza in the game scene, which is every existing village, so no data changes.
--
-- Run this by hand before the server build that adds TownHallX, TownHallZ and TownHallRotation to
-- Village ships. This project has no EF migrations, every save writes every property, and GameData
-- loads every entity set at boot, so the columns must exist first.
--
-- Safe to run twice.

SET NOCOUNT ON;

IF COL_LENGTH(N'dbo.Village', N'TownHallX') IS NULL
BEGIN
    ALTER TABLE dbo.Village ADD TownHallX float NULL;
    PRINT 'Added dbo.Village.TownHallX';
END

IF COL_LENGTH(N'dbo.Village', N'TownHallZ') IS NULL
BEGIN
    ALTER TABLE dbo.Village ADD TownHallZ float NULL;
    PRINT 'Added dbo.Village.TownHallZ';
END

IF COL_LENGTH(N'dbo.Village', N'TownHallRotation') IS NULL
BEGIN
    ALTER TABLE dbo.Village ADD TownHallRotation float NULL;
    PRINT 'Added dbo.Village.TownHallRotation';
END
