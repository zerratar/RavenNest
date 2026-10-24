using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game.Trading;
using RavenNest.DataModels;
using RavenNest.Models;
using Character = RavenNest.DataModels.Character;
using GameSession = RavenNest.DataModels.GameSession;
using Resources = RavenNest.DataModels.Resources;
using Village = RavenNest.DataModels.Village;

namespace RavenNest.BusinessLogic.Game.StreamerShop
{
    /// <summary>
    ///     The tavern shelves: a streamer puts items of their own up for coins, viewers buy them one at a
    ///     time, and every coin goes into the streamer's village, never to the streamer.
    /// </summary>
    /// <remarks>
    ///     Every move goes through <see cref="TradeExecutor"/>, so listing, buying and returning happen
    ///     completely or not at all. The listing row is the escrow (as a marketplace listing is): written
    ///     only after the items have left the source, and removed only after they have gone back.
    ///
    ///     <para>
    ///     See docs/streamer-shop-design.md in the game repository. First numbers set 2026-10-24, to be
    ///     balanced.
    ///     </para>
    /// </remarks>
    public class StreamerShopManager
    {
        public const int BaseShelves = 3;
        public const int MaxShelves = 8;
        public const long PriceCap = 100_000_000;
        public const int MinHours = 1;
        public const int MaxHours = 30 * 24;

        // loyalty points with the streamer for what a viewer spends: one per this many coins, at most this many a purchase
        public const long CoinsPerLoyaltyPoint = 1_000;
        public const long MaxLoyaltyPointsPerPurchase = 100;

        private readonly GameData gameData;
        private readonly PlayerInventoryProvider inventories;
        private readonly SessionManager sessionManager;
        private readonly ILogger<StreamerShopManager> logger;

        public StreamerShopManager(GameData gameData, PlayerInventoryProvider inventories, SessionManager sessionManager, ILogger<StreamerShopManager> logger)
        {
            this.gameData = gameData;
            this.inventories = inventories;
            this.sessionManager = sessionManager;
            this.logger = logger;
        }

        // ---- Reading -------------------------------------------------------------------------------

        public StreamerShopInfo GetShop(Guid sessionId)
        {
            var session = gameData.GetSession(sessionId);
            return session != null ? ShopOf(session) : null;
        }

        /// <summary>What the streamer could put on a shelf: the stash, and their characters in this stream.</summary>
        public StreamerShopSources GetSources(Guid sessionId)
        {
            var session = gameData.GetSession(sessionId);
            if (session == null) return null;

            var result = new StreamerShopSources();

            var stash = new StreamerShopSource { Kind = StreamerShopSourceKind.Stash, Name = "Stash" };
            foreach (var group in gameData.GetUserBankItems(session.UserId).GroupBy(x => x.Key()))
            {
                var first = group.First();
                stash.Items.Add(Sellable(group.Key, group.Sum(x => x.Amount), group.Any(x => x.Soulbound)));
            }
            result.Sources.Add(stash);

            foreach (var character in OwnCharacters(session))
            {
                var source = new StreamerShopSource { Kind = StreamerShopSourceKind.Character, CharacterId = character.Id, Name = character.Name };
                foreach (var group in inventories.Get(character.Id).GetUnequippedItems().GroupBy(x => x.Key()))
                {
                    source.Items.Add(Sellable(group.Key, group.Sum(x => x.Amount), group.Any(x => x.Soulbound)));
                }
                result.Sources.Add(source);
            }

            var clan = gameData.GetClanByOwner(session.UserId);
            if (clan != null)
            {
                result.HasClan = true;
                result.ClanRoles = gameData.GetClanRoles(clan.Id)
                    .OrderByDescending(x => x.Level)
                    .Select(x => new StreamerShopClanRole { Id = x.Id, Name = x.Name, Level = x.Level })
                    .ToList();
            }

            return result;
        }

        /// <summary>Every sale from this stream's shelves, newest first, with totals.</summary>
        public StreamerShopReport GetReport(Guid sessionId, int take = 200)
        {
            var session = gameData.GetSession(sessionId);
            if (session == null) return null;

            var sales = gameData.GetStreamerShopTransactions(session.UserId);
            var since = DateTime.UtcNow.AddHours(-24);
            return new StreamerShopReport
            {
                TotalCoins = sales.Sum(x => x.Price),
                TotalItems = sales.Sum(x => x.Amount),
                CoinsLast24Hours = sales.Where(x => x.Created >= since).Sum(x => x.Price),
                Sales = sales.OrderByDescending(x => x.Created).Take(take).Select(x => new StreamerShopSale
                {
                    Time = x.Created,
                    BuyerName = gameData.GetCharacter(x.BuyerCharacterId)?.Name ?? gameData.GetUser(x.BuyerUserId)?.UserName,
                    ItemId = x.ItemId,
                    ItemName = x.ItemName,
                    Amount = x.Amount,
                    Price = x.Price,
                    LoyaltyPoints = x.LoyaltyPoints
                }).ToList()
            };
        }

        // ---- The streamer: listing, changing, taking down ---------------------------------------------

        public StreamerShopResult List(Guid sessionId, StreamerShopListingRequest request)
        {
            var session = gameData.GetSession(sessionId);
            if (session == null) return Fail("The stream could not be found.");
            if (request == null) return Fail("Nothing to list.");

            var village = gameData.GetOrCreateVillageBySession(session);
            var shelves = ShelvesFor(village);
            if (request.Shelf < 0 || request.Shelf >= shelves) return Fail("That shelf is not in your tavern yet. Upgrade the town hall for more shelves.");
            if (gameData.GetStreamerShopListings(session.UserId).Any(x => x.Shelf == request.Shelf)) return Fail("Something is already on that shelf.");
            if (request.Amount <= 0) return Fail("Pick how many to put up.");

            var error = CheckTerms(session, request.PricePerItem, request.Access, request.AllowedClanRoleIds, request.MaxPerViewer);
            if (error != null) return Fail(error);

            var stack = new StackKey(request.ItemId, request.Tag, request.Enchantment, request.TransmogrificationId, request.Name, request.Flags);
            var world = new StreamerShopWorld(gameData, inventories);
            Guid source;

            if (request.SourceKind == StreamerShopSourceKind.Stash)
            {
                var rows = gameData.GetUserBankItems(session.UserId).Where(x => x.Key() == stack).ToList();
                var sellable = Sellable(stack, rows.Sum(x => x.Amount), rows.Any(x => x.Soulbound));
                if (!sellable.Sellable) return Fail(sellable.Reason);
                source = session.UserId;
                world.WithStash(source);
            }
            else
            {
                var character = OwnCharacters(session).FirstOrDefault(x => x.Id == request.SourceCharacterId);
                if (character == null) return Fail("That character is not yours, or not in this stream.");
                var inventory = inventories.Get(character.Id);
                var rows = inventory.GetUnequippedItems().Where(x => x.Key() == stack).ToList();
                var sellable = Sellable(stack, rows.Sum(x => x.Amount), rows.Any(x => x.Soulbound));
                if (!sellable.Sellable) return Fail(sellable.Reason);
                if (rows.Any(x => inventory.IsLocked(x.Id))) return Fail("That item is busy being saved. Try again in a moment.");
                source = character.Id;
                world.WithCharacter(source);
            }

            var plan = new TradePlan(TradeKind.StreamerShopListing, new[] { new ItemMove(source, ItemMove.Outside, stack, request.Amount) });
            var result = new TradeExecutor(world).Commit(plan);
            if (!result.Ok) return Fail(result.Reason);

            var now = DateTime.UtcNow;
            var listing = new StreamerShopListing
            {
                Id = Guid.NewGuid(),
                StreamerUserId = session.UserId,
                Shelf = request.Shelf,
                ItemId = stack.ItemId,
                Amount = request.Amount,
                PricePerItem = request.PricePerItem,
                Name = stack.Name,
                Enchantment = stack.Enchantment,
                Tag = stack.Tag,
                TransmogrificationId = stack.TransmogrificationId,
                Flags = stack.Flags,
                SourceKind = (int)request.SourceKind,
                SourceCharacterId = request.SourceKind == StreamerShopSourceKind.Character ? source : (Guid?)null,
                Access = (int)request.Access,
                AllowedClanRoleIds = JoinRoles(request.Access, request.AllowedClanRoleIds),
                MaxPerViewer = Math.Max(0, request.MaxPerViewer),
                Created = now,
                Expires = now.AddHours(Hours(request.DurationHours)),
            };
            gameData.Add(listing);

            SendItemEvents(world);
            SendShopUpdated(session);
            return new StreamerShopResult { Ok = true, Listing = ToInfo(listing), Shop = ShopOf(session) };
        }

        /// <summary>Changes the price, the time left (counted from now), who may buy and the limit per viewer.</summary>
        public StreamerShopResult Edit(Guid sessionId, Guid listingId, StreamerShopListingRequest request)
        {
            var session = gameData.GetSession(sessionId);
            if (session == null) return Fail("The stream could not be found.");
            var listing = gameData.GetStreamerShopListing(listingId);
            if (listing == null || listing.StreamerUserId != session.UserId) return Fail("That listing is not on your shelves.");
            if (request == null) return Fail("Nothing to change.");

            var error = CheckTerms(session, request.PricePerItem, request.Access, request.AllowedClanRoleIds, request.MaxPerViewer);
            if (error != null) return Fail(error);

            listing.PricePerItem = request.PricePerItem;
            listing.Access = (int)request.Access;
            listing.AllowedClanRoleIds = JoinRoles(request.Access, request.AllowedClanRoleIds);
            listing.MaxPerViewer = Math.Max(0, request.MaxPerViewer);
            if (request.DurationHours > 0) listing.Expires = DateTime.UtcNow.AddHours(Hours(request.DurationHours));

            SendShopUpdated(session);
            return new StreamerShopResult { Ok = true, Listing = ToInfo(listing), Shop = ShopOf(session) };
        }

        /// <summary>Takes a listing down; what is left goes back where it came from.</summary>
        public StreamerShopResult Remove(Guid sessionId, Guid listingId)
        {
            var session = gameData.GetSession(sessionId);
            if (session == null) return Fail("The stream could not be found.");
            var listing = gameData.GetStreamerShopListing(listingId);
            if (listing == null || listing.StreamerUserId != session.UserId) return Fail("That listing is not on your shelves.");

            var error = Return(listing);
            if (error != null) return Fail(error);

            SendShopUpdated(session);
            return new StreamerShopResult { Ok = true, Shop = ShopOf(session) };
        }

        // ---- Viewers: buying ----------------------------------------------------------------------------

        /// <summary>Buys one from a listing for a character playing in the stream.</summary>
        public StreamerShopBuyResult Buy(SessionToken token, Guid characterId, Guid listingId)
        {
            var session = gameData.GetSession(token.SessionId);
            if (session == null) return BuyFail(StreamerShopBuyResultCode.Error, "The stream could not be found.");

            var character = gameData.GetActiveSessionCharacters(session)?.FirstOrDefault(x => x.Id == characterId);
            if (character == null) return BuyFail(StreamerShopBuyResultCode.NotInSession, "You are not playing in this stream.");

            var listing = gameData.GetStreamerShopListing(listingId);
            if (listing == null || listing.StreamerUserId != session.UserId || listing.Expires <= DateTime.UtcNow)
                return BuyFail(StreamerShopBuyResultCode.NotFound, "That is no longer on the shelf.");
            if (listing.Amount <= 0) return BuyFail(StreamerShopBuyResultCode.SoldOut, "That shelf is sold out.");
            if (character.UserId == listing.StreamerUserId) return BuyFail(StreamerShopBuyResultCode.OwnListing, "You cannot buy from your own shelves.");

            var access = (StreamerShopAccess)listing.Access;
            if (access != StreamerShopAccess.Everyone)
            {
                var clan = gameData.GetClanByOwner(listing.StreamerUserId);
                var membership = gameData.GetClanMembership(character.Id);
                if (clan == null || membership == null || membership.ClanId != clan.Id)
                    return BuyFail(StreamerShopBuyResultCode.NotAllowed, "Only members of the streamer's clan can buy this.");
                if (access == StreamerShopAccess.ClanRoles && !SplitRoles(listing.AllowedClanRoleIds).Contains(membership.ClanRoleId))
                    return BuyFail(StreamerShopBuyResultCode.NotAllowed, "Your clan role cannot buy this.");
            }

            if (listing.MaxPerViewer > 0)
            {
                var bought = gameData.GetStreamerShopTransactionsByListing(listing.Id).Where(x => x.BuyerUserId == character.UserId).Sum(x => x.Amount);
                if (bought >= listing.MaxPerViewer) return BuyFail(StreamerShopBuyResultCode.LimitReached, "You have bought as many of these as you may.");
            }

            var village = gameData.GetOrCreateVillageBySession(session);
            var world = new StreamerShopWorld(gameData, inventories).WithCharacter(character.Id).WithVillage(village);
            var price = listing.PricePerItem;
            if (world.CoinsOf(character.Id) < price)
            {
                var poor = BuyFail(StreamerShopBuyResultCode.InsufficientCoins, "You need " + price + " coins.");
                poor.CoinsLeft = world.CoinsOf(character.Id);
                poor.Price = price;
                return poor;
            }

            var plan = new TradePlan(
                TradeKind.StreamerShopPurchase,
                new[] { new ItemMove(ItemMove.Outside, character.Id, listing.Key(), 1) },
                new[] { new CoinMove(character.Id, village.Id, price) });

            var trade = new TradeExecutor(world).Commit(plan);
            if (!trade.Ok)
            {
                logger.LogWarning("Shop purchase refused for character '" + character.Id + "' on listing '" + listing.Id + "': " + trade.Reason);
                return BuyFail(StreamerShopBuyResultCode.Error, trade.Reason);
            }

            listing.Amount -= 1;
            listing.Sold += 1;
            listing.CoinsEarned += price;

            var points = Math.Min(MaxLoyaltyPointsPerPurchase, price / CoinsPerLoyaltyPoint);
            if (points > 0)
            {
                var loyalty = gameData.GetUserLoyalty(character.UserId, listing.StreamerUserId) ?? CreateLoyalty(character.UserId, listing.StreamerUserId);
                loyalty.Points += points;
            }

            var itemName = listing.Name ?? gameData.GetItem(listing.ItemId)?.Name;
            gameData.Add(new StreamerShopTransaction
            {
                Id = Guid.NewGuid(),
                ListingId = listing.Id,
                StreamerUserId = listing.StreamerUserId,
                BuyerUserId = character.UserId,
                BuyerCharacterId = character.Id,
                ItemId = listing.ItemId,
                ItemName = itemName,
                Amount = 1,
                Price = price,
                LoyaltyPoints = points,
                Created = DateTime.UtcNow
            });

            var result = new StreamerShopBuyResult
            {
                Code = StreamerShopBuyResultCode.Success,
                ListingId = listing.Id,
                Shelf = listing.Shelf,
                ItemId = listing.ItemId,
                ItemName = itemName,
                InventoryItemId = world.Added.FirstOrDefault().Row?.Id ?? Guid.Empty,
                Price = price,
                CoinsLeft = world.CoinsOf(character.Id),
                LoyaltyPoints = points,
                AmountLeft = listing.Amount
            };

            // a sold out listing has nothing left to hold; its sales stay in the transactions
            if (listing.Amount <= 0) gameData.Remove(listing);

            SendItemEvents(world);
            SendCoins(session, character);
            SendVillageInfo(session);
            SendShopUpdated(session);
            return result;
        }

        // ---- Expiry ----------------------------------------------------------------------------------------

        /// <summary>Returns every listing that has run out of time. Survives single failures.</summary>
        public int ReturnExpiredListings()
        {
            var now = DateTime.UtcNow;
            var returned = 0;
            var streamers = new HashSet<Guid>();
            foreach (var listing in gameData.GetAllStreamerShopListings().Where(x => x.Expires <= now).ToList())
            {
                try
                {
                    var error = Return(listing);
                    if (error != null)
                    {
                        logger.LogError("Could not return expired shop listing '" + listing.Id + "': " + error);
                        continue;
                    }

                    returned++;
                    streamers.Add(listing.StreamerUserId);
                }
                catch (Exception exc)
                {
                    logger.LogError("Returning expired shop listing '" + listing.Id + "' failed: " + exc);
                }
            }

            foreach (var streamer in streamers)
            {
                var session = gameData.GetSessionByUserId(streamer);
                if (session != null) SendShopUpdated(session);
            }

            return returned;
        }

        // ---- Helpers ---------------------------------------------------------------------------------------

        /// <summary>
        ///     Puts what is left of a listing back where it came from: the character it was taken from,
        ///     or the stash when that character is gone or cannot take it. Then removes the listing.
        /// </summary>
        private string Return(StreamerShopListing listing)
        {
            if (listing.Amount > 0)
            {
                var stack = listing.Key();
                var delivered = false;
                StreamerShopWorld world = null;

                if (listing.SourceKind == (int)StreamerShopSourceKind.Character && listing.SourceCharacterId is Guid characterId)
                {
                    var character = gameData.GetCharacter(characterId);
                    if (character != null && character.UserId == listing.StreamerUserId)
                    {
                        world = new StreamerShopWorld(gameData, inventories).WithCharacter(characterId);
                        var plan = new TradePlan(TradeKind.StreamerShopReturn, new[] { new ItemMove(ItemMove.Outside, characterId, stack, listing.Amount) });
                        delivered = new TradeExecutor(world).Commit(plan).Ok;
                    }
                }

                if (!delivered)
                {
                    world = new StreamerShopWorld(gameData, inventories).WithStash(listing.StreamerUserId);
                    var plan = new TradePlan(TradeKind.StreamerShopReturn, new[] { new ItemMove(ItemMove.Outside, listing.StreamerUserId, stack, listing.Amount) });
                    var result = new TradeExecutor(world).Commit(plan);
                    if (!result.Ok) return result.Reason;
                }

                SendItemEvents(world);
            }

            gameData.Remove(listing);
            return null;
        }

        private string CheckTerms(GameSession session, long price, StreamerShopAccess access, List<Guid> roleIds, int maxPerViewer)
        {
            if (price < 1) return "The price must be at least 1 coin.";
            if (price > PriceCap) return "The price can be at most " + PriceCap.ToString("N0") + " coins.";
            if (maxPerViewer < 0) return "The limit per viewer cannot be below zero.";

            if (access == StreamerShopAccess.Everyone) return null;
            if (access != StreamerShopAccess.Clan && access != StreamerShopAccess.ClanRoles) return "Pick who may buy.";

            var clan = gameData.GetClanByOwner(session.UserId);
            if (clan == null) return "You need a clan of your own to limit a listing to clan members.";
            if (access == StreamerShopAccess.ClanRoles)
            {
                var roles = gameData.GetClanRoles(clan.Id).Select(x => x.Id).ToHashSet();
                if (roleIds == null || roleIds.Count == 0) return "Pick at least one clan role.";
                if (roleIds.Any(x => !roles.Contains(x))) return "One of those roles is not in your clan.";
            }

            return null;
        }

        private StreamerShopSellableItem Sellable(StackKey stack, long amount, bool instanceSoulbound)
        {
            var item = gameData.GetItem(stack.ItemId);
            var result = new StreamerShopSellableItem
            {
                ItemId = stack.ItemId,
                ItemName = item?.Name,
                Amount = amount,
                Tag = stack.Tag,
                Enchantment = stack.Enchantment,
                TransmogrificationId = stack.TransmogrificationId,
                Name = stack.Name,
                Flags = stack.Flags,
                VendorPrice = item?.ShopSellPrice ?? 0,
                Sellable = true
            };

            string reason = null;
            if (item == null) reason = "This item no longer exists.";
            else if (amount <= 0) reason = "None left.";
            else if (item.Soulbound || instanceSoulbound) reason = "Soulbound items cannot be sold.";
            else if (stack.TransmogrificationId != null) reason = "Transmogrified items cannot be sold.";
            else if (item.Category == (int)DataModels.ItemCategory.StreamerToken || item.Type == (int)DataModels.ItemType.StreamerToken) reason = "Tokens cannot be sold.";

            if (reason != null)
            {
                result.Sellable = false;
                result.Reason = reason;
            }

            return result;
        }

        /// <summary>The streamer's own characters playing in their stream.</summary>
        private IEnumerable<Character> OwnCharacters(GameSession session) =>
            (gameData.GetActiveSessionCharacters(session) ?? (IReadOnlyList<Character>)Array.Empty<Character>()).Where(x => x.UserId == session.UserId);

        /// <summary>3 shelves, one more per town hall tier (shown from 1), at most 8.</summary>
        public static int ShelvesFor(Village village)
        {
            var tier = village?.TownHallTier ?? TownHallTiers.FromLevel(village?.Level ?? 1);
            return Math.Min(MaxShelves, BaseShelves + tier + 1);
        }

        private static int Hours(int requested) => Math.Clamp(requested <= 0 ? 24 : requested, MinHours, MaxHours);

        private static string JoinRoles(StreamerShopAccess access, List<Guid> roleIds) =>
            access == StreamerShopAccess.ClanRoles && roleIds != null ? string.Join(",", roleIds.Distinct()) : null;

        private static HashSet<Guid> SplitRoles(string roleIds) =>
            string.IsNullOrEmpty(roleIds)
                ? new HashSet<Guid>()
                : roleIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => Guid.TryParse(x, out var id) ? id : Guid.Empty).Where(x => x != Guid.Empty).ToHashSet();

        private StreamerShopInfo ShopOf(GameSession session)
        {
            var village = gameData.GetOrCreateVillageBySession(session);
            var resources = village != null ? gameData.GetResources(village.ResourcesId) : null;
            return new StreamerShopInfo
            {
                Shelves = ShelvesFor(village),
                VillageCoins = resources != null ? (long)Math.Floor(resources.Coins) : 0,
                PriceCap = PriceCap,
                MinHours = MinHours,
                MaxHours = MaxHours,
                Listings = gameData.GetStreamerShopListings(session.UserId).OrderBy(x => x.Shelf).Select(ToInfo).ToList()
            };
        }

        private static StreamerShopListingInfo ToInfo(StreamerShopListing x) => new StreamerShopListingInfo
        {
            Id = x.Id,
            Shelf = x.Shelf,
            ItemId = x.ItemId,
            Amount = x.Amount,
            PricePerItem = x.PricePerItem,
            Name = x.Name,
            Enchantment = x.Enchantment,
            Tag = x.Tag,
            TransmogrificationId = x.TransmogrificationId,
            Flags = x.Flags,
            SourceKind = (StreamerShopSourceKind)x.SourceKind,
            SourceCharacterId = x.SourceCharacterId,
            Access = (StreamerShopAccess)x.Access,
            AllowedClanRoleIds = SplitRoles(x.AllowedClanRoleIds).ToList(),
            MaxPerViewer = x.MaxPerViewer,
            Sold = x.Sold,
            CoinsEarned = x.CoinsEarned,
            Created = x.Created,
            Expires = x.Expires
        };

        private UserLoyalty CreateLoyalty(Guid userId, Guid streamerUserId)
        {
            var loyalty = new UserLoyalty
            {
                Id = Guid.NewGuid(),
                Playtime = "00:00:00",
                Points = 0,
                Experience = 0,
                StreamerUserId = streamerUserId,
                UserId = userId,
                Level = 1,
                CheeredBits = 0,
                GiftedSubs = 0
            };
            gameData.Add(loyalty);
            return loyalty;
        }

        // ---- Telling the game -----------------------------------------------------------------------------

        private void SendShopUpdated(GameSession session)
        {
            gameData.EnqueueGameEvent(gameData.CreateSessionEvent(GameEventType.StreamerShopUpdated, session, ShopOf(session)));
        }

        private void SendVillageInfo(GameSession session) => sessionManager.SendVillageInfo(session);

        private void SendCoins(GameSession session, Character character)
        {
            var resources = gameData.GetResources(character);
            if (resources == null) return;
            gameData.EnqueueGameEvent(gameData.CreateSessionEvent(GameEventType.ResourceUpdate, session, new ResourceUpdate
            {
                CharacterId = character.Id,
                FishAmount = resources.Fish,
                OreAmount = resources.Ore,
                WheatAmount = resources.Wheat,
                WoodAmount = resources.Wood,
                CoinsAmount = resources.Coins
            }));
        }

        /// <summary>Tells each character's game which inventory rows changed.</summary>
        private void SendItemEvents(StreamerShopWorld world)
        {
            if (world == null) return;

            foreach (var taken in world.Taken)
            {
                var session = SessionOf(taken.CharacterId);
                if (session == null) continue;
                gameData.EnqueueGameEvent(gameData.CreateSessionEvent(GameEventType.ItemRemove, session, new ItemRemove
                {
                    PlayerId = taken.CharacterId,
                    InventoryItemId = taken.InventoryItemId,
                    ItemId = taken.ItemId,
                    Amount = taken.Amount
                }));
            }

            foreach (var added in world.Added)
            {
                var session = SessionOf(added.CharacterId);
                if (session == null) continue;
                var row = added.Row;
                gameData.EnqueueGameEvent(gameData.CreateSessionEvent(GameEventType.ItemAdd, session, new ItemAdd
                {
                    PlayerId = added.CharacterId,
                    Amount = added.Amount,
                    ItemId = row.ItemId,
                    InventoryItemId = row.Id,
                    Name = row.Name,
                    Enchantment = row.Enchantment,
                    TransmogrificationId = row.TransmogrificationId,
                    Flags = row.Flags.GetValueOrDefault(),
                    Tag = row.Tag,
                    Soulbound = row.Soulbound
                }));
            }
        }

        private GameSession SessionOf(Guid characterId)
        {
            var character = gameData.GetCharacter(characterId);
            return character?.UserIdLock is Guid owner ? gameData.GetSessionByUserId(owner) : null;
        }

        private static StreamerShopResult Fail(string error) => new StreamerShopResult { Ok = false, Error = error };

        private static StreamerShopBuyResult BuyFail(StreamerShopBuyResultCode code, string error) =>
            new StreamerShopBuyResult { Code = code, Error = error };
    }
}
