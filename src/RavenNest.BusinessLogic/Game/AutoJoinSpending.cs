using RavenNest.BusinessLogic.Data;
using System;
using System.Globalization;
using System.Linq;

namespace RavenNest.BusinessLogic.Game
{
    /// <summary>
    /// What an account pays for joining raids and dungeons automatically, per day (UTC), its patron perks for it, and
    /// the account's own limit.
    ///
    /// Each join costs <see cref="PlayerManager.AutoJoinRaidCost"/> or <see cref="PlayerManager.AutoJoinDungeonCost"/>.
    /// Patrons get some auto joins free every day (raids and dungeons counted apart) and a discount on the rest, more
    /// with each tier (<see cref="Perks"/>). For scale (active accounts, 2026-11-20): the median auto joining account
    /// joined about 22 a day, a quarter over 42 and a tenth over 146. So Mithril's free joins cover about half of a
    /// median day, Dragon's all of it, and Phantom's a busy one; dungeons cost more, so fewer of them are free.
    ///
    /// A player can set their own daily limit (the website's dashboard, or !auto limit in chat): reaching it stops paid
    /// auto joins for the rest of the day. A player on a stream running raids and dungeons back to back lost 17 million
    /// coins in 12 days to auto join (2026-11) without seeing it go.
    ///
    /// Coins belong to the account, so all its characters share both. Kept in user properties so the 12 hour restarts
    /// do not reset them (a counter kept only in memory charged ten times over once, see 85792ede).
    /// </summary>
    public sealed class AutoJoinSpending
    {
        public const int HistoryDays = 7;
        public const string SpendKey = "autojoin.spend";
        public const string LimitKey = "autojoin.limit";

        /// <summary>A tier's auto join perk: free raids and free dungeons a day, and the discount on the rest.</summary>
        public readonly record struct Perk(int FreeRaids, int FreeDungeons, int DiscountPercent);

        /// <summary>By Patreon tier: none, Mithril, Rune, Dragon, Abraxas, Phantom.</summary>
        public static readonly Perk[] Perks =
        {
            new Perk(0, 0, 0),
            new Perk(6, 4, 10),
            new Perk(10, 7, 20),
            new Perk(15, 10, 30),
            new Perk(20, 14, 40),
            new Perk(25, 18, 50),
        };

        public static Perk PerkFor(int? patreonTier) => Perks[Math.Clamp(patreonTier ?? 0, 0, Perks.Length - 1)];

        /// <summary>What one auto join costs after the tier's discount.</summary>
        public static long PriceFor(long cost, int? patreonTier) => (long)Math.Round(cost * (100 - PerkFor(patreonTier).DiscountPercent) / 100d);

        /// <summary>What a join came to: a code per character, in the order asked.</summary>
        public enum Result
        {
            NotEnoughCoins = 0,
            Paid = 1,
            Free = 2,
            PersonalLimitReached = 3,
        }

        private readonly GameData gameData;
        private readonly object mutex = new object();

        public AutoJoinSpending(GameData gameData)
        {
            this.gameData = gameData;
        }

        /// <summary>
        /// One auto join for the account: free while it has free joins of that kind left today (patrons), otherwise
        /// the discounted cost, refused when the account's own limit would be passed or the coins are short.
        /// </summary>
        public Result Charge(Guid userId, RavenNest.DataModels.Resources resources, long cost, bool raid)
        {
            lock (mutex)
            {
                var tier = gameData.GetUser(userId)?.PatreonTier;
                var perk = PerkFor(tier);
                var spend = Read(userId);
                if (raid ? spend.FreeRaids < perk.FreeRaids : spend.FreeDungeons < perk.FreeDungeons)
                {
                    if (raid) spend.FreeRaids++; else spend.FreeDungeons++;
                    Write(userId, spend);
                    return Result.Free;
                }

                if (resources == null) return Result.NotEnoughCoins;
                var price = PriceFor(cost, tier);
                var limit = GetLimit(userId);
                if (limit != null && spend.Today + price > limit.Value) return Result.PersonalLimitReached;
                if (resources.Coins < price) return Result.NotEnoughCoins;

                resources.Coins -= price;
                spend.Today += price;
                Write(userId, spend);
                return Result.Paid;
            }
        }

        /// <summary>
        /// { paid today, paid in the last 7 days (today included), own limit (-1 none), free raids left today, free
        /// dungeons left today, free raids a day, free dungeons a day, discount percent, raid price, dungeon price }.
        /// </summary>
        public long[] Status(Guid userId)
        {
            lock (mutex)
            {
                var tier = gameData.GetUser(userId)?.PatreonTier;
                var perk = PerkFor(tier);
                var spend = Read(userId);
                return new[]
                {
                    spend.Today, spend.Today + spend.History.Sum(), GetLimit(userId) ?? -1,
                    Math.Max(0, perk.FreeRaids - spend.FreeRaids), Math.Max(0, perk.FreeDungeons - spend.FreeDungeons),
                    perk.FreeRaids, perk.FreeDungeons, perk.DiscountPercent,
                    PriceFor(PlayerManager.AutoJoinRaidCost, tier), PriceFor(PlayerManager.AutoJoinDungeonCost, tier),
                };
            }
        }

        /// <summary>The account's own daily limit, or null for none (0 stops every paid auto join).</summary>
        public long? GetLimit(Guid userId)
        {
            var value = gameData.GetUserProperty(userId, LimitKey);
            return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var limit) && limit >= 0 ? limit : null;
        }

        public void SetLimit(Guid userId, long? limit)
        {
            gameData.SetUserProperty(userId, LimitKey, limit is long l && l >= 0 ? l.ToString(CultureInfo.InvariantCulture) : "");
        }

        /// <summary>
        /// Reads a limit as a player types it: 100000, 100k, 1.5m, 100,000; "off", "none" or "remove" for no limit.
        /// False when it is not one of those.
        /// </summary>
        public static bool TryParseLimit(string text, out long? limit)
        {
            limit = null;
            text = (text ?? "").Trim().ToLowerInvariant().Replace(",", "").Replace(" ", "");
            if (text is "off" or "none" or "remove" or "clear" or "no" or "unlimited") return true;
            var factor = 1d;
            if (text.EndsWith("k")) { factor = 1_000; text = text[..^1]; }
            else if (text.EndsWith("m")) { factor = 1_000_000; text = text[..^1]; }
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || value < 0 || double.IsNaN(value)) return false;
            limit = (long)Math.Min(long.MaxValue / 2, Math.Round(value * factor));
            return true;
        }

        private sealed class Spend
        {
            public int Day;
            public long Today;
            public long[] History = new long[HistoryDays - 1]; // yesterday first
            public int FreeRaids;
            public int FreeDungeons;
        }

        private static int DayNumber(DateTime utc) => (int)(utc.Date - DateTime.UnixEpoch).TotalDays;

        // "day|paid today|yesterday,the day before,...|free raids used|free dungeons used", day the UTC day number;
        // rolled forward to today on reading (a new day: today's payments go into the history, free joins start over)
        private Spend Read(Guid userId)
        {
            var today = DayNumber(DateTime.UtcNow);
            var spend = new Spend { Day = today };
            var value = gameData.GetUserProperty(userId, SpendKey);
            if (string.IsNullOrEmpty(value)) return spend;
            var parts = value.Split('|');
            if (parts.Length < 2 || !int.TryParse(parts[0], out var day) || !long.TryParse(parts[1], out var paid)) return spend;
            var history = parts.Length > 2
                ? parts[2].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => long.TryParse(x, out var v) ? v : 0).ToArray()
                : Array.Empty<long>();
            var gap = today - day;
            if (gap <= 0)
            {
                spend.Today = paid;
                for (var i = 0; i < history.Length && i < spend.History.Length; i++) spend.History[i] = history[i];
                if (parts.Length > 3) int.TryParse(parts[3], out spend.FreeRaids);
                if (parts.Length > 4) int.TryParse(parts[4], out spend.FreeDungeons);
                return spend;
            }
            if (gap - 1 < spend.History.Length) spend.History[gap - 1] = paid;
            for (var i = 0; i < history.Length; i++) if (i + gap < spend.History.Length) spend.History[i + gap] = history[i];
            return spend;
        }

        private void Write(Guid userId, Spend spend)
        {
            gameData.SetUserProperty(userId, SpendKey, string.Join("|",
                spend.Day.ToString(CultureInfo.InvariantCulture),
                spend.Today.ToString(CultureInfo.InvariantCulture),
                string.Join(",", spend.History.Select(x => x.ToString(CultureInfo.InvariantCulture))),
                spend.FreeRaids.ToString(CultureInfo.InvariantCulture),
                spend.FreeDungeons.ToString(CultureInfo.InvariantCulture)));
        }
    }
}
