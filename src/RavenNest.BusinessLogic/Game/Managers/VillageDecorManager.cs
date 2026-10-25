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
    /// <para>
    /// Phase 3 (decided 2026-10-25): seasonal pieces unlock for good for a village that streams during their event
    /// (<see cref="Events"/>); the Special pieces are unlocked once with the treasury's coins; viewers give pieces
    /// with their loyalty points with the streamer (<see cref="Gift"/>), priced by the piece's worth. A gift waits
    /// to be placed, is placed for free, and goes back to the gifts when removed instead of paying out.
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

            // phase 3 (2026-10-25): seasonal pieces, unlocked for good by streaming during their event
            Piece("xmas_tree_01", "Christmas tree", "Christmas", wood: 80, coins: 10, ev: "christmas"),
            Piece("snowman_01", "Snowman", "Christmas", wood: 10, wheat: 10, ev: "christmas"),
            Piece("snowman_02", "Tall snowman", "Christmas", wood: 10, wheat: 15, ev: "christmas"),
            Piece("sleigh_01", "Santa's sleigh", "Christmas", wood: 150, coins: 50, ev: "christmas"),
            Piece("gift_pile_01", "Gift pile", "Christmas", wheat: 10, coins: 20, ev: "christmas"),
            Piece("nutcracker_01", "Nutcracker", "Christmas", wood: 40, coins: 10, ev: "christmas"),
            Piece("candy_cane_01", "Giant candy cane", "Christmas", wheat: 30, ev: "christmas"),
            Piece("gingerbread_hut_01", "Gingerbread hut", "Christmas", wheat: 60, ev: "christmas"),
            Piece("snow_globe_01", "Giant snow globe", "Christmas", ore: 40, coins: 20, ev: "christmas"),

            Piece("pumpkin_01", "Pumpkin", "Halloween", wheat: 10, ev: "halloween"),
            Piece("pumpkin_02", "Big pumpkin", "Halloween", wheat: 25, ev: "halloween"),
            Piece("gravestone_01", "Gravestone", "Halloween", ore: 20, ev: "halloween"),
            Piece("gravestone_03", "Tall gravestone", "Halloween", ore: 30, ev: "halloween"),
            Piece("grave_01", "Grave", "Halloween", wood: 10, ore: 15, ev: "halloween"),
            Piece("scarecrow_01", "Scarecrow", "Halloween", wood: 20, wheat: 10, ev: "halloween"),
            Piece("cauldron_01", "Cauldron", "Halloween", ore: 40, ev: "halloween"),
            Piece("coffin_01", "Coffin", "Halloween", wood: 40, ev: "halloween"),
            Piece("bones_01", "Bones", "Halloween", ore: 5, ev: "halloween", small: true),

            // and the Special pieces, unlocked once with the village treasury's coins (what the tavern shelves earn)
            Piece("fountain_01", "Grand fountain", "Special", ore: 300, coins: 100, tier: 2, unlock: 2500),
            Piece("fountain_02", "Wall fountain", "Special", ore: 200, coins: 50, tier: 1, unlock: 1500),
            Piece("garden_fountain_01", "Garden fountain", "Special", ore: 150, coins: 30, tier: 1, unlock: 1200),
            Piece("obelisk_01", "Obelisk", "Special", ore: 250, coins: 50, tier: 2, unlock: 1500),
            Piece("obelisk_02", "Floating obelisk", "Special", ore: 500, coins: 200, tier: 4, unlock: 4000),
            Piece("pillar_monument_01", "Great pillar", "Special", ore: 400, coins: 150, tier: 3, unlock: 3000),

            // quests, phase 3 (2026-10-25): a village gets the piece when one of its viewers finishes the chain there
            Piece("cat_01", "Sir Whiskers", "Quest", wheat: 20, quest: "cat"),
            Piece("fish_trophy_01", "The one that got away", "Quest", ore: 60, wood: 20, quest: "fish"),
            Piece("statue_founder_01", "Founder's statue", "Quest", ore: 300, coins: 50, quest: "founder"),
            Piece("boat_build_01", "Boat in the making", "Quest", wood: 200, ore: 40, quest: "shipwright"),
            Piece("jack_o_lantern_01", "Jack-o'-lantern", "Quest", wheat: 25, quest: "pumpkin"),
        };

        /// <summary>
        /// The seasonal events and when they run (day of the year, from and to, both included). A village whose decor
        /// loads while one runs, which the game does at every start, unlocks its pieces for good.
        /// </summary>
        public static readonly (string Key, int FromMonth, int FromDay, int ToMonth, int ToDay)[] Events =
        {
            ("halloween", 10, 15, 11, 5),
            ("christmas", 12, 1, 1, 6),
        };

        public static bool IsRunning(string ev, DateTime now)
        {
            foreach (var e in Events)
            {
                if (e.Key != ev) continue;
                var day = now.Month * 100 + now.Day;
                var from = e.FromMonth * 100 + e.FromDay;
                var to = e.ToMonth * 100 + e.ToDay;
                return from <= to ? day >= from && day <= to : day >= from || day <= to; // christmas runs over new year
            }
            return false;
        }

        /// <summary>
        /// What a viewer pays in loyalty points to give a piece: about one point per two resources it costs, coins
        /// counting double, in steps of five, at least 50 (decided 2026-10-25).
        /// </summary>
        public static long GiftPoints(VillageDecorPiece p)
        {
            var worth = p.Wood + p.Ore + p.Fish + p.Wheat + p.Coins * 2;
            return Math.Max(50, (long)Math.Round(worth / 2 / 5) * 5);
        }

        private static VillageDecorPiece Piece(string key, string name, string group, double wood = 0, double ore = 0, double wheat = 0, double coins = 0, int tier = 0, int level = 0, string ev = null, double unlock = 0, bool small = false, string quest = null) =>
            new VillageDecorPiece { Key = key, Name = name, Group = group, Tier = tier, Level = level, Event = ev, UnlockCoins = unlock, Small = small, Quest = quest, Wood = wood, Ore = ore, Wheat = wheat, Coins = coins };

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
            if (village != null) UnlockRunningEvents(village);
            var unlocks = village != null ? UnlocksOf(village) : new HashSet<string>();
            var gifts = village != null ? gameData.GetVillageDecorGifts(village.Id) : Array.Empty<VillageDecorGift>();
            return Pieces.Select(p =>
            {
                var gift = gifts.FirstOrDefault(g => g.Piece == p.Key);
                return new VillageDecorPiece
                {
                    Key = p.Key, Name = p.Name, Group = p.Group, Small = p.Small, Tier = p.Tier, Level = p.Level,
                    Event = p.Event, UnlockCoins = p.UnlockCoins, Quest = p.Quest, Points = GiftPoints(p),
                    Gifts = gift?.Amount ?? 0, GiftedBy = gift?.Givers,
                    Unlocked = village != null && IsUnlocked(village, p, unlocks),
                    Wood = p.Wood, Ore = p.Ore, Fish = p.Fish, Wheat = p.Wheat, Coins = p.Coins,
                };
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

            var resources = gameData.GetResources(village.ResourcesId);
            if (resources == null) return Fail("unknown");

            lock (mutex)
            {
                // a gift is the viewer's: free, and placeable whatever the village has unlocked since
                VillageDecorGift gift = null;
                if (request.Gift)
                {
                    gift = gameData.GetVillageDecorGifts(village.Id).FirstOrDefault(g => g.Piece == piece.Key);
                    if (gift == null || gift.Amount <= 0) return Fail("nogift", resources);
                    gift.Amount--;
                    gift.Updated = DateTime.UtcNow;
                }
                else
                {
                    if (!IsUnlocked(village, piece, UnlocksOf(village))) return Fail("locked", resources);
                    if (!CanPay(resources, piece)) return Fail("resources", resources);
                    Pay(resources, piece);
                }

                var paid = gift == null;
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
                    Gift = !paid,
                    PaidWood = paid ? piece.Wood : 0, PaidOre = paid ? piece.Ore : 0, PaidFish = paid ? piece.Fish : 0,
                    PaidWheat = paid ? piece.Wheat : 0, PaidCoins = paid ? piece.Coins : 0,
                    Created = DateTime.UtcNow,
                };
                gameData.Add(decor);
                if (gift != null) gameData.Touch();
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
                if (decor != null && decor.Gift)
                {
                    // a gift goes back with the gifts, to be placed again; nothing was paid, nothing comes back
                    wood = ore = fish = wheat = coins = 0;
                    AddGift(village, decor.Piece, null);
                    gameData.Remove(decor);
                }
                else if (decor != null && string.IsNullOrEmpty(decor.DefaultKey))
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

        /// <summary>
        /// A viewer finished a quest chain in this stream: the chain's piece is the village's for good. Returns the
        /// piece's key, or null when the chain has none or the village had it already.
        /// </summary>
        public string UnlockForQuest(Guid sessionId, string chain)
        {
            var village = VillageOf(sessionId);
            var piece = Pieces.FirstOrDefault(p => p.Quest == chain);
            if (village == null || piece == null) return null;
            lock (mutex)
            {
                if (UnlocksOf(village).Contains(piece.Key)) return null;
                gameData.Add(new VillageDecorUnlock { Id = Guid.NewGuid(), VillageId = village.Id, Key = piece.Key, Source = "quest", Created = DateTime.UtcNow });
                return piece.Key;
            }
        }

        /// <summary>A Special piece unlocked for good with the village treasury's coins.</summary>
        public VillageDecorResult Unlock(Guid sessionId, VillageDecorRequest request)
        {
            var village = VillageOf(sessionId);
            if (village == null) return Fail("unknown");
            var piece = Pieces.FirstOrDefault(p => p.Key == request?.Piece);
            if (piece == null || piece.UnlockCoins <= 0) return Fail("invalid");
            var resources = gameData.GetResources(village.ResourcesId);
            if (resources == null) return Fail("unknown");

            lock (mutex)
            {
                var unlocks = UnlocksOf(village);
                if (unlocks.Contains(piece.Key)) return Fail("unlocked", resources);
                if (TierOf(village) < piece.Tier || village.Level < piece.Level) return Fail("locked", resources);
                if (resources.Coins < piece.UnlockCoins) return Fail("resources", resources);

                resources.Coins -= piece.UnlockCoins;
                gameData.Add(new VillageDecorUnlock
                {
                    Id = Guid.NewGuid(),
                    VillageId = village.Id,
                    Key = piece.Key,
                    Source = "treasury",
                    PaidCoins = piece.UnlockCoins,
                    Created = DateTime.UtcNow,
                });
                var result = new VillageDecorResult { Ok = true };
                Fill(result, resources);
                return result;
            }
        }

        /// <summary>
        /// A viewer gives the streamer's village a piece with their loyalty points with that streamer. Any piece the
        /// village has unlocked can be given; it waits with the gifts until the streamer places it.
        /// </summary>
        public VillageDecorGiftResult Gift(Guid sessionId, VillageDecorGiftRequest request)
        {
            var session = gameData.GetSession(sessionId);
            var village = VillageOf(sessionId);
            if (session == null || village == null || request == null) return new VillageDecorGiftResult { Error = "invalid" };

            var piece = Pieces.FirstOrDefault(p => p.Key == request.Piece);
            if (piece == null) return new VillageDecorGiftResult { Error = "unknown" };
            var result = new VillageDecorGiftResult { Piece = piece.Key, PieceName = piece.Name, Points = GiftPoints(piece) };
            if (!IsUnlocked(village, piece, UnlocksOf(village))) { result.Error = "locked"; return result; }

            var user = !string.IsNullOrEmpty(request.PlatformId) ? gameData.GetUser(request.PlatformId, request.Platform) : null;
            if (user == null) { result.Error = "nouser"; return result; }

            lock (mutex)
            {
                var loyalty = gameData.GetUserLoyalty(user.Id, session.UserId);
                var points = loyalty?.Points ?? 0;
                result.PointsLeft = points;
                if (loyalty == null || points < result.Points) { result.Error = "points"; return result; }

                loyalty.Points -= result.Points;
                result.PointsLeft = loyalty.Points;
                var name = !string.IsNullOrEmpty(request.UserName) ? request.UserName : user.UserName;
                result.Gifts = AddGift(village, piece.Key, name);
                gameData.Add(new VillageDecorGiftLog
                {
                    Id = Guid.NewGuid(),
                    VillageId = village.Id,
                    Piece = piece.Key,
                    UserId = user.Id,
                    UserName = name,
                    Points = result.Points,
                    Created = DateTime.UtcNow,
                });
                result.Ok = true;

                // the giver's character in this stream counts it (the Generous achievement)
                QuestTracker.DecorGifted(gameData.GetActiveSessionCharacters(session).FirstOrDefault(c => c.UserId == user.Id));
                return result;
            }
        }

        // ---- Helpers -------------------------------------------------------------------------------

        /// <summary>One more of a piece with the gifts; the giver's name goes first in the list. How many there are now.</summary>
        private long AddGift(Village village, string piece, string giver)
        {
            var gift = gameData.GetVillageDecorGifts(village.Id).FirstOrDefault(g => g.Piece == piece);
            if (gift == null)
            {
                gift = new VillageDecorGift { Id = Guid.NewGuid(), VillageId = village.Id, Piece = piece, Created = DateTime.UtcNow };
                gameData.Add(gift);
            }

            gift.Amount++;
            if (!string.IsNullOrEmpty(giver))
            {
                var names = new List<string> { giver };
                if (!string.IsNullOrEmpty(gift.Givers))
                {
                    names.AddRange(gift.Givers.Split(',').Where(n => !string.IsNullOrEmpty(n) && !n.Equals(giver, StringComparison.OrdinalIgnoreCase)));
                }
                gift.Givers = string.Join(",", names.Take(5));
            }
            gift.Updated = DateTime.UtcNow;
            gameData.Touch();
            return gift.Amount;
        }

        /// <summary>The events and treasury pieces the village has unlocked.</summary>
        private HashSet<string> UnlocksOf(Village village) =>
            new HashSet<string>(gameData.GetVillageDecorUnlocks(village.Id).Select(u => u.Key), StringComparer.Ordinal);

        private bool IsUnlocked(Village village, VillageDecorPiece piece, HashSet<string> unlocks)
        {
            if (TierOf(village) < piece.Tier || village.Level < piece.Level) return false;
            if (!string.IsNullOrEmpty(piece.Event) && !unlocks.Contains(piece.Event)) return false;
            if (piece.UnlockCoins > 0 && !unlocks.Contains(piece.Key)) return false;
            if (!string.IsNullOrEmpty(piece.Quest) && !unlocks.Contains(piece.Key)) return false;
            return true;
        }

        /// <summary>The village streams during an event: its pieces are the village's for good.</summary>
        private void UnlockRunningEvents(Village village)
        {
            var now = DateTime.UtcNow;
            HashSet<string> unlocks = null;
            foreach (var e in Events)
            {
                if (!IsRunning(e.Key, now)) continue;
                unlocks ??= UnlocksOf(village);
                if (unlocks.Contains(e.Key)) continue;
                lock (mutex)
                {
                    if (UnlocksOf(village).Contains(e.Key)) continue;
                    gameData.Add(new VillageDecorUnlock { Id = Guid.NewGuid(), VillageId = village.Id, Key = e.Key, Source = "event", Created = now });
                }
            }
        }

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
            Gift = decor.Gift,
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
