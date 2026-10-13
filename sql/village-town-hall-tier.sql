-- Town hall upgrades (build mode, town hall step B).
--
-- Adds the town hall tier a village has bought, 0 (the longhouse) to 4 (the castle). Null means the
-- village has not been seen by a client that knows about upgrades yet; the server then stores the tier
-- its level gave it before, so no village loses its hall. No data changes here.
--
-- Run this by hand before the server build that adds TownHallTier to Village ships. This project has no
-- EF migrations, every save writes every property, and GameData loads every entity set at boot, so the
-- column must exist first.
--
-- Safe to run twice.

-- Since DatabaseSchema (src/RavenNest.BusinessLogic/Data/DatabaseSchema.cs), the server adds missing
-- columns itself at startup, so running this by hand is no longer required. It is kept as a record of
-- the change and for adding the columns ahead of a deploy.
--

SET NOCOUNT ON;

IF COL_LENGTH(N'dbo.Village', N'TownHallTier') IS NULL
BEGIN
    ALTER TABLE dbo.Village ADD TownHallTier int NULL;
    PRINT 'Added dbo.Village.TownHallTier';
END
