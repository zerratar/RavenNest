using System;
using System.Globalization;
using System.Linq;
using System.Text;
using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;
using RavenNest.Models;
using Resources = RavenNest.DataModels.Resources;

namespace RavenNest.BusinessLogic.Game
{
    /// <summary>
    /// The roads a streamer draws in their village: what each type costs and when it unlocks,
    /// and building, changing and removing them with the village's resources.
    /// </summary>
    /// <remarks>
    /// The game client draws the curve, the road and the warnings (a road through a building is
    /// shown red, never moved or removed). The server keeps the centre line, checks the type is
    /// unlocked, and charges for the length.
    ///
    /// <para>
    /// Costs: building pays for the length; changing a road pays for metres added and gives back
    /// metres taken away, at the price of the type it ends up as, so reshaping never costs more
    /// than the difference; removing gives back half of what was paid.
    /// </para>
    /// <para>
    /// A road can be wider or narrower at each point (MinWidth to MaxWidth times the type's width),
    /// so the length it pays for is weighted by width: a road twice as wide costs twice as much per
    /// metre. Its ends can be round or square; that costs nothing.
    /// </para>
    /// </remarks>
    public class VillageRoadManager
    {
        public const int MaxPoints = 128;
        public const double MaxLength = 2000;
        public const double RemoveRefund = 0.5;
        public const double MinWidth = 0.5;
        public const double MaxWidth = 2.0;

        // The price list, per metre of road. Tiers count from 0 like the town hall tiers
        // (TownHallTiers), shown in game as one higher. A road unlocks with the town hall whose ground is made of it
        // (decided 2026-10-25): dirt like the first halls' yards, paving like the third hall's forecourt and the
        // fourth's plaza, cobbles like the fifth's plaza. Gravel, which no hall has, comes with the second.
        // First numbers, set 2026-10-14, to be balanced.
        public static readonly VillageRoadType[] Types =
        {
            new VillageRoadType { Id = 0, Name = "Dirt", Width = 2.5, Tier = 0, Wood = 2 },
            new VillageRoadType { Id = 1, Name = "Gravel", Width = 3.0, Tier = 1, Ore = 4 },
            new VillageRoadType { Id = 3, Name = "Paving", Width = 3.5, Tier = 2, Ore = 6, Coins = 1 },
            new VillageRoadType { Id = 2, Name = "Cobbles", Width = 3.5, Tier = 4, Ore = 8, Coins = 2 },
        };

        private readonly GameData gameData;

        public VillageRoadManager(GameData gameData)
        {
            this.gameData = gameData;
        }

        public VillageRoadType[] GetTypes(Guid sessionId)
        {
            var village = VillageOf(sessionId);
            var tier = village != null ? TierOf(village) : 0;
            return Types.Select(t => new VillageRoadType
            {
                Id = t.Id, Name = t.Name, Width = t.Width, Tier = t.Tier, Unlocked = tier >= t.Tier,
                Wood = t.Wood, Ore = t.Ore, Fish = t.Fish, Wheat = t.Wheat, Coins = t.Coins,
            }).ToArray();
        }

        public VillageRoadInfo[] GetRoads(Guid sessionId)
        {
            var village = VillageOf(sessionId);
            if (village == null) return Array.Empty<VillageRoadInfo>();
            return gameData.GetVillageRoads(village.Id).Select(Info).ToArray();
        }

        public VillageRoadResult Build(Guid sessionId, VillageRoadRequest request)
        {
            var village = VillageOf(sessionId);
            if (village == null) return Fail("unknown");

            var type = Unlocked(village, request?.Type ?? -1, out var error);
            if (type == null) return Fail(error);

            var points = Clean(request.Points);
            if (points == null) return Fail("invalid");
            var widths = CleanWidths(request.Widths, points.Length / 2);
            if (widths == null) return Fail("invalid");

            var resources = gameData.GetResources(village.ResourcesId);
            if (resources == null) return Fail("unknown");

            var length = Length(points);
            var cost = Cost(type, PaidLength(points, widths));
            if (!CanPay(resources, cost)) return Fail("resources", resources);

            Pay(resources, cost);
            var road = new VillageRoad
            {
                Id = Guid.NewGuid(),
                VillageId = village.Id,
                Type = type.Id,
                Points = Format(points),
                Widths = FormatWidths(widths),
                StartRound = Round(request.StartRound),
                EndRound = Round(request.EndRound),
                Length = length,
                Created = DateTime.UtcNow,
            };
            Remember(road, cost);
            gameData.Add(road);
            return Done(road, resources);
        }

        public VillageRoadResult Change(Guid sessionId, Guid roadId, VillageRoadRequest request)
        {
            var village = VillageOf(sessionId);
            var road = gameData.GetVillageRoad(roadId);
            if (village == null || road == null || road.VillageId != village.Id) return Fail("unknown");

            var type = Unlocked(village, request?.Type ?? -1, out var error);
            if (type == null) return Fail(error);

            var points = Clean(request.Points);
            if (points == null) return Fail("invalid");
            var widths = CleanWidths(request.Widths, points.Length / 2);
            if (widths == null) return Fail("invalid");

            var resources = gameData.GetResources(village.ResourcesId);
            if (resources == null) return Fail("unknown");

            // what the road would cost now, against what was paid: pay the difference, or get it back
            var length = Length(points);
            var cost = Cost(type, PaidLength(points, widths));
            var difference = new Price(cost.Wood - road.PaidWood, cost.Ore - road.PaidOre, cost.Fish - road.PaidFish,
                cost.Wheat - road.PaidWheat, cost.Coins - road.PaidCoins);
            var due = difference.Positive();
            if (!CanPay(resources, due)) return Fail("resources", resources);

            Pay(resources, due);
            Refund(resources, difference.Negative(), 1.0);

            road.Type = type.Id;
            road.Points = Format(points);
            road.Widths = FormatWidths(widths);
            road.StartRound = Round(request.StartRound ?? road.StartRound);
            road.EndRound = Round(request.EndRound ?? road.EndRound);
            road.Length = length;
            road.Updated = DateTime.UtcNow;
            Remember(road, cost);
            gameData.Touch();
            return Done(road, resources);
        }

        public VillageRoadResult Remove(Guid sessionId, Guid roadId)
        {
            var village = VillageOf(sessionId);
            var road = gameData.GetVillageRoad(roadId);
            if (village == null || road == null || road.VillageId != village.Id) return Fail("unknown");

            var resources = gameData.GetResources(village.ResourcesId);
            if (resources != null)
            {
                Refund(resources, new Price(road.PaidWood, road.PaidOre, road.PaidFish, road.PaidWheat, road.PaidCoins), RemoveRefund);
            }

            gameData.Remove(road);
            return Done(null, resources);
        }

        // ---- Helpers -------------------------------------------------------------------------------

        private Village VillageOf(Guid sessionId)
        {
            var session = gameData.GetSession(sessionId);
            return session != null ? gameData.GetOrCreateVillageBySession(session) : null;
        }

        private static int TierOf(Village village) => village.TownHallTier ?? TownHallTiers.FromLevel(village.Level);

        private static VillageRoadType Unlocked(Village village, int typeId, out string error)
        {
            var type = Types.FirstOrDefault(t => t.Id == typeId);
            if (type == null) { error = "invalid"; return null; }
            if (TierOf(village) < type.Tier) { error = "locked"; return null; }
            error = null;
            return type;
        }

        /// <summary>The points as (x, z) pairs, or null when they cannot make a road.</summary>
        private static double[] Clean(double[] points)
        {
            if (points == null || points.Length < 4 || points.Length % 2 != 0 || points.Length > MaxPoints * 2) return null;
            if (points.Any(p => double.IsNaN(p) || double.IsInfinity(p) || Math.Abs(p) > 100_000)) return null;
            var length = Length(points);
            if (length < 0.5 || length > MaxLength) return null;
            return points.Select(p => Math.Round(p, 2)).ToArray();
        }

        /// <summary>
        /// One width per point, clamped to MinWidth to MaxWidth and rounded; all 1 when none were sent.
        /// Null when they were sent but do not match the points.
        /// </summary>
        private static double[] CleanWidths(double[] widths, int count)
        {
            if (widths == null || widths.Length == 0) return Enumerable.Repeat(1.0, count).ToArray();
            if (widths.Length != count || widths.Any(w => double.IsNaN(w) || double.IsInfinity(w))) return null;
            return widths.Select(w => Math.Round(Math.Clamp(w, MinWidth, MaxWidth), 2)).ToArray();
        }

        /// <summary>1 for a half circle, 0 for a square end; nothing sent is round.</summary>
        private static double? Round(double? round) => round.HasValue ? Math.Round(Math.Clamp(round.Value, 0.0, 1.0), 2) : (double?)null;

        /// <summary>The length paid for: each stretch between two points, times their average width.</summary>
        private static double PaidLength(double[] points, double[] widths)
        {
            var length = 0.0;
            for (var i = 1; i < points.Length / 2; ++i)
            {
                var dx = points[i * 2] - points[i * 2 - 2];
                var dz = points[i * 2 + 1] - points[i * 2 - 1];
                length += Math.Sqrt(dx * dx + dz * dz) * (widths[i - 1] + widths[i]) / 2.0;
            }
            return length;
        }

        private static double Length(double[] points)
        {
            var length = 0.0;
            for (var i = 2; i + 1 < points.Length; i += 2)
            {
                var dx = points[i] - points[i - 2];
                var dz = points[i + 1] - points[i - 1];
                length += Math.Sqrt(dx * dx + dz * dz);
            }
            return length;
        }

        private readonly struct Price
        {
            public readonly double Wood, Ore, Fish, Wheat, Coins;
            public Price(double wood, double ore, double fish, double wheat, double coins)
            {
                Wood = wood; Ore = ore; Fish = fish; Wheat = wheat; Coins = coins;
            }
            public Price Positive() => new Price(Math.Max(0, Wood), Math.Max(0, Ore), Math.Max(0, Fish), Math.Max(0, Wheat), Math.Max(0, Coins));
            public Price Negative() => new Price(Math.Max(0, -Wood), Math.Max(0, -Ore), Math.Max(0, -Fish), Math.Max(0, -Wheat), Math.Max(0, -Coins));
        }

        // whole units: a road of 7.3 m costs what 8 m does
        private static Price Cost(VillageRoadType type, double length)
        {
            double Of(double perMetre) => Math.Ceiling(perMetre * length);
            return new Price(Of(type.Wood), Of(type.Ore), Of(type.Fish), Of(type.Wheat), Of(type.Coins));
        }

        private static bool CanPay(Resources r, Price p) =>
            r.Wood >= p.Wood && r.Ore >= p.Ore && r.Fish >= p.Fish && r.Wheat >= p.Wheat && r.Coins >= p.Coins;

        private static void Pay(Resources r, Price p)
        {
            r.Wood -= p.Wood; r.Ore -= p.Ore; r.Fish -= p.Fish; r.Wheat -= p.Wheat; r.Coins -= p.Coins;
        }

        private static void Refund(Resources r, Price p, double share)
        {
            r.Wood += Math.Floor(p.Wood * share);
            r.Ore += Math.Floor(p.Ore * share);
            r.Fish += Math.Floor(p.Fish * share);
            r.Wheat += Math.Floor(p.Wheat * share);
            r.Coins += Math.Floor(p.Coins * share);
        }

        private static void Remember(VillageRoad road, Price paid)
        {
            road.PaidWood = paid.Wood; road.PaidOre = paid.Ore; road.PaidFish = paid.Fish;
            road.PaidWheat = paid.Wheat; road.PaidCoins = paid.Coins;
        }

        private static string Format(double[] points)
        {
            var sb = new StringBuilder();
            for (var i = 0; i + 1 < points.Length; i += 2)
            {
                if (i > 0) sb.Append(';');
                sb.Append(points[i].ToString("0.##", CultureInfo.InvariantCulture)).Append(',')
                  .Append(points[i + 1].ToString("0.##", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        private static string FormatWidths(double[] widths)
        {
            // 1 everywhere is the default; nothing stored keeps old roads and new plain ones alike
            if (widths.All(w => Math.Abs(w - 1.0) < 0.001)) return null;
            return string.Join(";", widths.Select(w => w.ToString("0.##", CultureInfo.InvariantCulture)));
        }

        private static double[] ParseWidths(string widths)
        {
            if (string.IsNullOrEmpty(widths)) return null;
            return widths.Split(';')
                .Select(v => double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 1)
                .ToArray();
        }

        private static double[] Parse(string points)
        {
            if (string.IsNullOrEmpty(points)) return Array.Empty<double>();
            return points.Split(';')
                .SelectMany(pair => pair.Split(','))
                .Select(v => double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0)
                .ToArray();
        }

        private static VillageRoadInfo Info(VillageRoad road) => new VillageRoadInfo
        {
            Id = road.Id,
            Type = road.Type,
            Points = Parse(road.Points),
            Widths = ParseWidths(road.Widths),
            StartRound = road.StartRound ?? 1,
            EndRound = road.EndRound ?? 1,
        };

        private static VillageRoadResult Fail(string error, Resources resources = null)
        {
            var result = new VillageRoadResult { Ok = false, Error = error };
            Fill(result, resources);
            return result;
        }

        private static VillageRoadResult Done(VillageRoad road, Resources resources)
        {
            var result = new VillageRoadResult { Ok = true, Road = road != null ? Info(road) : null };
            Fill(result, resources);
            return result;
        }

        private static void Fill(VillageRoadResult result, Resources r)
        {
            if (r == null) return;
            result.Wood = r.Wood; result.Ore = r.Ore; result.Fish = r.Fish; result.Wheat = r.Wheat; result.Coins = r.Coins;
        }
    }
}
