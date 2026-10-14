-- Village house default layout freeze (build mode, village area by town hall tier).
--
-- The game scene's default plot positions are moving: the old layout spread the 40 plots over the
-- whole island, the new one fills outward from the plaza, so the area each town hall tier allows
-- means something. A house with no stored position stands at its plot's scene default, so without
-- this every existing house that was never moved would jump to the new layout.
--
-- This stores the OLD default position for every built house (Type > 0) that has none, so existing
-- villages keep exactly what they have. Empty plots are left alone: they take the new layout.
--
-- Run it by hand, with the server stopped, BEFORE the game client with the new layout ships. The
-- server keeps every entity in memory and writes all properties back, so running it while the
-- server is up would be undone. Safe to run twice: it only touches rows still without a position.
--
-- Positions are world x and z in the game scene, rotation is degrees about y. Generated from
-- Ravenfall Legacy tools/village-rebuild/layout/old-defaults.txt.

SET NOCOUNT ON;

DECLARE @OldDefaults TABLE (Slot int PRIMARY KEY, X float NOT NULL, Z float NOT NULL, Rotation float NOT NULL);
INSERT INTO @OldDefaults (Slot, X, Z, Rotation) VALUES
    (0, -36.10, -13.10, 0.0),
    (1, 16.70, -50.30, 0.0),
    (2, -57.20, -43.80, 0.0),
    (3, -28.80, -51.40, 0.0),
    (4, -89.60, -15.70, 0.0),
    (5, -71.40, -10.90, 0.0),
    (6, 46.00, -11.70, 0.0),
    (7, 54.70, -57.80, 0.0),
    (8, 26.60, -11.20, 0.0),
    (9, -82.90, -39.30, 0.0),
    (10, 58.20, -89.60, 0.0),
    (11, 59.10, -108.70, 0.0),
    (12, -4.67, -74.50, 0.0),
    (13, 14.10, -133.80, 0.0),
    (14, -52.90, -10.90, 0.0),
    (15, 36.80, -139.10, 0.0),
    (16, 41.80, -45.00, 0.0),
    (17, 9.90, -13.10, 0.0),
    (18, 33.70, -77.70, 0.0),
    (19, 11.30, -96.90, 0.0),
    (20, -27.10, -2.60, 0.0),
    (21, -98.80, -44.20, 0.0),
    (22, 42.55, -89.27, 0.0),
    (23, -110.80, -50.50, 0.0),
    (24, -42.70, -46.60, 0.0),
    (25, 26.00, -37.70, 0.0),
    (26, 27.48, -147.29, 0.0),
    (27, 42.30, -107.90, 0.0),
    (28, 15.95, -115.41, 0.0),
    (29, 45.10, -126.40, 0.0),
    (30, -77.13, -126.61, 0.0),
    (31, -60.00, -128.20, 0.0),
    (32, -2.50, -163.73, 0.0),
    (33, -38.33, -130.79, 0.0),
    (34, -21.85, -164.52, 0.0),
    (35, -46.99, -144.33, 0.0),
    (36, -80.83, -105.20, 0.0),
    (37, -66.45, -73.43, 0.0),
    (38, -58.00, -89.24, 0.0),
    (39, -81.99, -82.89, 0.0);

UPDATE house
SET house.PositionX = d.X,
    house.PositionZ = d.Z,
    house.Rotation = d.Rotation
FROM dbo.VillageHouse AS house
INNER JOIN @OldDefaults AS d ON d.Slot = house.Slot
WHERE house.PositionX IS NULL
  AND house.Type > 0;

PRINT CONCAT('Froze ', @@ROWCOUNT, ' houses at their old default positions');
