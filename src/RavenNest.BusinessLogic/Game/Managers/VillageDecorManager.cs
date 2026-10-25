using System;
using System.Collections.Generic;
using System.Linq;
using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;
using RavenNest.Models;
using Resources = RavenNest.DataModels.Resources;

namespace RavenNest.BusinessLogic.Game
{
    /// <summary>
    /// A village's decor: the pieces a streamer places in build mode, and the island's own trees, bushes and
    /// flowers, which they can move and remove. See docs/village-decor-design.md in the game repository.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only changes are kept. A placed piece is a row; an island piece gets a row once it is moved or removed. A
    /// village that never touches its decor has none and looks like the island as shipped, and changes to the
    /// island's pieces reach every village without moving anything someone placed.
    /// </para>
    /// <para>
    /// Costs, decided 2026-10-24: placing pays the piece's price; removing gives back half of its worth, the
    /// island's own pieces included (half of their price, since nobody paid for them), so a new streamer can clear
    /// some of the island for a start in resources. Removing and placing again always loses half, and an island
    /// piece can only be removed once: only keys in <see cref="VillageDecorDefaults"/> count, so nothing can be
    /// removed that is not there. No limit on how much decor a village has: the price is the only brake.
    /// </para>
    /// </remarks>
    public class VillageDecorManager
    {
        public const double RemoveRefund = 0.5;
        private const double MaxCoordinate = 100_000;

        // The price list. Tiers count from 0 like the town hall tiers, shown in game as one higher. What a piece is
        // made of decides what it costs: wood for trees and bushes, wheat for flowers and grass, ore for stone.
        // First numbers, set 2026-10-24, to be balanced.
        public static readonly VillageDecorPiece[] Pieces =
        {
            Tree("tree_02", "Round tree", 60), Tree("tree_03", "Leafy tree", 60), Tree("tree_04", "Tall tree", 60),
            Tree("tree_large_01", "Great tree", 120, tier: 1), Tree("tree_large_02", "Broad tree", 120, tier: 1), Tree("tree_large_03", "Old tree", 120, tier: 1),
            Tree("tree_thin_01", "Slender tree", 40), Tree("tree_thin_02", "Young tree", 40),
            Tree("tree_bush_01", "Bushy tree", 30), Tree("tree_bush_02", "Shrub tree", 30),
            Bush("bush_cluster_01", "Bushes"), Bush("bush_cluster_02", "Hedge"), Bush("bush_cluster_03", "Low bushes"),
            Bush("bush_cluster_04", "Thick bushes"), Bush("bush_cluster_05", "Wild bushes"),
            Flowers("flowers_01", "Wild flowers", 10), Flowers("flowers_02", "Meadow flowers", 10),
            Flowers("flowers_04", "Flower bed", 10), Flowers("flowers_07", "Blossoms", 10),
            Flowers("grass_01", "Grass", 5), Flowers("grass_02", "Tall grass", 5), Flowers("grass_03", "Fern grass", 5),
            Rock("rock_01", "Boulder", 40), Rock("rock_02", "Rock", 50), Rock("rock_03", "Stones", 15, small: true),

            // phase 2 (2026-10-25): props for a lived-in village. The finest also need the village's level.
            Piece("fence_01", "Wooden fence", "Fences", wood: 12), Piece("fence_02", "Plank fence", "Fences", wood: 12),
            Piece("fence_03", "Rail fence", "Fences", wood: 12), Piece("fence_04", "Post fence", "Fences", wood: 12),
            Piece("fence_gate_01", "Gate", "Fences", wood: 20),
            Piece("archway_01", "Gateway", "Fences", wood: 30),
            Piece("archway_02", "Wide gateway", "Fences", wood: 50, tier: 1),

            Piece("torch_01", "Torch", "Lights", wood: 10), Piece("brazier_01", "Brazier", "Lights", ore: 25),
            Piece("brazier_02", "Large brazier", "Lights", ore: 70, tier: 1),
            Piece("street_lamp_01", "Street lamp", "Lights", ore: 40, coins: 10, tier: 1),
            Piece("street_lamp_02", "Double lamp", "Lights", ore: 50, coins: 15, tier: 2),

            Piece("market_stall_01", "Market stall", "Market", wood: 60, coins: 20, tier: 1),
            Piece("market_stall_07", "Small stall", "Market", wood: 60, coins: 20, tier: 1),
            Piece("market_stall_03", "Awning stall", "Market", wood: 80, coins: 30, tier: 1),
            Piece("market_stall_05", "Large stall", "Market", wood: 100, coins: 40, tier: 2),
            Piece("barrel_01", "Barrel", "Market", wood: 10), Piece("barrel_stack_01", "Barrel rack", "Market", wood: 30),
            Piece("crate_wood_01", "Crate", "Market", wood: 10), Piece("crate_stack_01", "Crates", "Market", wood: 30),
            Piece("sack_stack_01", "Sacks", "Market", wheat: 15), Piece("table_barrel_01", "Barrel table", "Market", wood: 15),

            Piece("hay_bale_01", "Hay bale", "Farm", wheat: 10), Piece("hay_pile_01", "Haystack", "Farm", wheat: 25),
            Piece("hay_cart_01", "Hay cart", "Farm", wood: 40, wheat: 20, tier: 1),
            Piece("log_01", "Log", "Farm", wood: 10), Piece("ground_logs_01", "Log pile", "Farm", wood: 30),
            Piece("planterbox_preset_01", "Planter", "Farm", wood: 15, wheat: 10),
            Piece("planterbox_small_preset_01", "Small planter", "Farm", wood: 10, wheat: 5),

            Piece("well_01", "Well", "Village", ore: 80, tier: 1), Piece("direction_sign_01", "Signpost", "Village", wood: 15),
            Piece("grinding_wheel_01", "Grinding wheel", "Village", ore: 30), Piece("anvil_01", "Anvil", "Village", ore: 40),
            Piece("bench_seat_01", "Long bench", "Village", wood: 25), Piece("bench_seat_02", "Bench", "Village", wood: 20),

            Piece("flag_pole_01", "Flagpole", "Banners", wood: 30, coins: 10, tier: 1),
            Piece("battle_banner_01", "Banner", "Banners", wood: 20, coins: 10),

            Piece("statue_knight_01", "Knight statue", "Statues", ore: 300, coins: 100, tier: 2, level: 50),
            Piece("statue_hero_01", "Hero statue", "Statues", ore: 500, coins: 250, tier: 3, level: 100),
            Piece("statue_angel_01", "Angel statue", "Statues", ore: 800, coins: 500, tier: 4, level: 200),
        };

        private static VillageDecorPiece Piece(string key, string name, string group, double wood = 0, double ore = 0, double wheat = 0, double coins = 0, int tier = 0, int level = 0) =>
            new VillageDecorPiece { Key = key, Name = name, Group = group, Tier = tier, Level = level, Wood = wood, Ore = ore, Wheat = wheat, Coins = coins };

        private static VillageDecorPiece Tree(string key, string name, double wood, int tier = 0) =>
            new VillageDecorPiece { Key = key, Name = name, Group = "Trees", Tier = tier, Wood = wood };
        private static VillageDecorPiece Bush(string key, string name) =>
            new VillageDecorPiece { Key = key, Name = name, Group = "Bushes", Wood = 20 };
        private static VillageDecorPiece Flowers(string key, string name, double wheat) =>
            new VillageDecorPiece { Key = key, Name = name, Group = "Flowers", Small = true, Wheat = wheat };
        private static VillageDecorPiece Rock(string key, string name, double ore, bool small = false) =>
            new VillageDecorPiece { Key = key, Name = name, Group = "Rocks", Small = small, Ore = ore };

        private readonly GameData gameData;
        private readonly object mutex = new object();

        public VillageDecorManager(GameData gameData)
        {
            this.gameData = gameData;
        }

        public VillageDecorPiece[] GetPieces(Guid sessionId)
        {
            var village = VillageOf(sessionId);
            var tier = village != null ? TierOf(village) : 0;
            var level = village?.Level ?? 0;
            return Pieces.Select(p => new VillageDecorPiece
            {
                Key = p.Key, Name = p.Name, Group = p.Group, Small = p.Small, Tier = p.Tier, Level = p.Level,
                Unlocked = tier >= p.Tier && level >= p.Level,
                Wood = p.Wood, Ore = p.Ore, Fish = p.Fish, Wheat = p.Wheat, Coins = p.Coins,
            }).ToArray();
        }

        public VillageDecorInfo[] GetDecor(Guid sessionId)
        {
            var village = VillageOf(sessionId);
            if (village == null) return Array.Empty<VillageDecorInfo>();
            return gameData.GetVillageDecor(village.Id).Select(Info).ToArray();
        }

        public VillageDecorResult Place(Guid sessionId, VillageDecorRequest request)
        {
            var village = VillageOf(sessionId);
            if (village == null) return Fail("unknown");

            var piece = Pieces.FirstOrDefault(p => p.Key == request?.Piece);
            if (piece == null || !ValidPlace(request)) return Fail("invalid");
            if (TierOf(village) < piece.Tier || village.Level < piece.Level) return Fail("locked");

            var resources = gameData.GetResources(village.ResourcesId);
            if (resources == null) return Fail("unknown");

            lock (mutex)
            {
                if (!CanPay(resources, piece)) return Fail("resources", resources);
                Pay(resources, piece);
                var decor = new VillageDecor
                {
                    Id = Guid.NewGuid(),
                    VillageId = village.Id,
                    Piece = piece.Key,
                    X = Math.Round(request.X, 2),
                    Y = Math.Round(request.Y, 2),
                    Z = Math.Round(request.Z, 2),
                    Rotation = Angle(request.Rotation),
                    Scale = Scale(request.Scale),
                    PaidWood = piece.Wood, PaidOre = piece.Ore, PaidFish = piece.Fish, PaidWheat = piece.Wheat, PaidCoins = piece.Coins,
                    Created = DateTime.UtcNow,
                };
                gameData.Add(decor);
                return Done(decor, resources);
            }
        }

        /// <summary>Moving costs nothing: a placed piece by its id, an island piece by its key.</summary>
        public VillageDecorResult Move(Guid sessionId, VillageDecorRequest request)
        {
            var village = VillageOf(sessionId);
            if (village == null) return Fail("unknown");
            if (request == null || !ValidPlace(request)) return Fail("invalid");
            var resources = gameData.GetResources(village.ResourcesId);

            lock (mutex)
            {
                var decor = Find(village, request, out var islandPiece);
                if (decor == null && islandPiece == null) return Fail("unknown");
                if (decor != null && decor.Removed) return Fail("removed");

                if (decor == null)
                {
                    decor = new VillageDecor
                    {
                        Id = Guid.NewGuid(),
                        VillageId = village.Id,
                        Piece = islandPiece,
                        DefaultKey = request.DefaultKey,
                        Scale = 0, // the island piece keeps its own size
                        Created = DateTime.UtcNow,
                    };
                    gameData.Add(decor);
                }

                decor.X = Math.Round(request.X, 2);
                decor.Y = Math.Round(request.Y, 2);
                decor.Z = Math.Round(request.Z, 2);
                decor.Rotation = Angle(request.Rotation);
                decor.Updated = DateTime.UtcNow;
                gameData.Touch();
                return Done(decor, resources);
            }
        }

        /// <summary>
        /// Gives back half its worth. A placed piece goes; an island piece is marked removed, which pays out once.
        /// </summary>
        public VillageDecorResult Remove(Guid sessionId, VillageDecorRequest request)
        {
            var village = VillageOf(sessionId);
            if (village == null || request == null) return Fail("unknown");
            var resources = gameData.GetResources(village.ResourcesId);
            if (resources == null) return Fail("unknown");

            lock (mutex)
            {
                var decor = Find(village, request, out var islandPiece);
                if (decor == null && islandPiece == null) return Fail("unknown");
                if (decor != null && decor.Removed) return Fail("removed");

                double wood, ore, fish, wheat, coins;
                if (decor != null && string.IsNullOrEmpty(decor.DefaultKey))
                {
                    wood = decor.PaidWood; ore = decor.PaidOre; fish = decor.PaidFish; wheat = decor.PaidWheat; coins = decor.PaidCoins;
                    gameData.Remove(decor);
                }
                else
                {
                    // nobody paid for the island's pieces: half of what the piece costs today
                    var piece = Pieces.FirstOrDefault(p => p.Key == (decor?.Piece ?? islandPiece));
                    wood = piece?.Wood ?? 0; ore = piece?.Ore ?? 0; fish = piece?.Fish ?? 0; wheat = piece?.Wheat ?? 0; coins = piece?.Coins ?? 0;
                    if (decor == null)
                    {
                        decor = new VillageDecor
                        {
                            Id = Guid.NewGuid(),
                            VillageId = village.Id,
                            Piece = islandPiece,
                            DefaultKey = request.DefaultKey,
                            Created = DateTime.UtcNow,
                        };
                        gameData.Add(decor);
                    }
                    decor.Removed = true;
                    decor.Updated = DateTime.UtcNow;
                    gameData.Touch();
                }

                var result = new VillageDecorResult
                {
                    Ok = true,
                    Decor = Info(decor),
                    RefundWood = Math.Floor(wood * RemoveRefund),
                    RefundOre = Math.Floor(ore * RemoveRefund),
                    RefundFish = Math.Floor(fish * RemoveRefund),
                    RefundWheat = Math.Floor(wheat * RemoveRefund),
                    RefundCoins = Math.Floor(coins * RemoveRefund),
                };
                resources.Wood += result.RefundWood;
                resources.Ore += result.RefundOre;
                resources.Fish += result.RefundFish;
                resources.Wheat += result.RefundWheat;
                resources.Coins += result.RefundCoins;
                Fill(result, resources);
                return result;
            }
        }

        // ---- Helpers -------------------------------------------------------------------------------

        /// <summary>
        /// The row the request means: a placed piece by id, or an island piece's row by key. For an island piece
        /// without a row yet, returns null and sets <paramref name="islandPiece"/> to what it is.
        /// </summary>
        private VillageDecor Find(Village village, VillageDecorRequest request, out string islandPiece)
        {
            islandPiece = null;
            var rows = gameData.GetVillageDecor(village.Id);
            if (request.Id.HasValue && request.Id.Value != Guid.Empty)
            {
                return rows.FirstOrDefault(x => x.Id == request.Id.Value);
            }

            if (string.IsNullOrEmpty(request.DefaultKey) || !VillageDecorDefaults.Pieces.TryGetValue(request.DefaultKey, out var piece))
            {
                return null;
            }

            var row = rows.FirstOrDefault(x => x.DefaultKey == request.DefaultKey);
            if (row == null) islandPiece = piece;
            return row;
        }

        private Village VillageOf(Guid sessionId)
        {
            var session = gameData.GetSession(sessionId);
            return session != null ? gameData.GetOrCreateVillageBySession(session) : null;
        }

        private static int TierOf(Village village) => village.TownHallTier ?? TownHallTiers.FromLevel(village.Level);

        private static bool ValidPlace(VillageDecorRequest r)
        {
            foreach (var v in new[] { r.X, r.Y, r.Z, r.Rotation })
            {
                if (double.IsNaN(v) || double.IsInfinity(v) || Math.Abs(v) > MaxCoordinate) return false;
            }
            return true;
        }

        private static double Angle(double degrees)
        {
            var a = degrees % 360.0;
            if (a < 0) a += 360.0;
            return Math.Round(a, 1);
        }

        private static double Scale(double scale) =>
            double.IsNaN(scale) || scale <= 0 ? 1 : Math.Round(Math.Clamp(scale, 0.5, 2.0), 2);

        private static bool CanPay(Resources r, VillageDecorPiece p) =>
            r.Wood >= p.Wood && r.Ore >= p.Ore && r.Fish >= p.Fish && r.Wheat >= p.Wheat && r.Coins >= p.Coins;

        private static void Pay(Resources r, VillageDecorPiece p)
        {
            r.Wood -= p.Wood; r.Ore -= p.Ore; r.Fish -= p.Fish; r.Wheat -= p.Wheat; r.Coins -= p.Coins;
        }

        private static VillageDecorInfo Info(VillageDecor decor) => new VillageDecorInfo
        {
            Id = decor.Id,
            Piece = decor.Piece,
            DefaultKey = decor.DefaultKey,
            X = decor.X,
            Y = decor.Y,
            Z = decor.Z,
            Rotation = decor.Rotation,
            Scale = decor.Scale,
            Removed = decor.Removed,
        };

        private static VillageDecorResult Fail(string error, Resources resources = null)
        {
            var result = new VillageDecorResult { Ok = false, Error = error };
            Fill(result, resources);
            return result;
        }

        private static VillageDecorResult Done(VillageDecor decor, Resources resources)
        {
            var result = new VillageDecorResult { Ok = true, Decor = Info(decor) };
            Fill(result, resources);
            return result;
        }

        private static void Fill(VillageDecorResult result, Resources r)
        {
            if (r == null) return;
            result.Wood = r.Wood; result.Ore = r.Ore; result.Fish = r.Fish; result.Wheat = r.Wheat; result.Coins = r.Coins;
        }
    }
}
