using System;
using System.Collections.Generic;
using System.Linq;
using RavenNest.BusinessLogic;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;

namespace RavenNest.Blazor.Services.Knowledge
{
    /// <summary>
    ///     Facts generated from the code that decides them, at every startup.
    /// </summary>
    /// <remarks>
    ///     A lot of what players ask is written down nowhere except in a constant. The wiki has
    ///     thirty articles and six are stubs, so this fills a real gap.
    ///
    ///     <para>
    ///     Every number here is <em>read</em>, never copied. That is the whole point. A fact written
    ///     as text is a copy of a value, and when the value changes the copy does not, so the
    ///     assistant states the old number confidently with an authoritative looking source. Worse
    ///     than not knowing: "I do not know" sends somebody to ask a person, a wrong number does
    ///     not.
    ///     </para>
    ///
    ///     <para>
    ///     There is a second guarantee, and it is the best part. These reference the constants in
    ///     C#, so deleting or renaming one breaks the build. A derived fact cannot outlive the rule
    ///     it describes, and it fails at compile time rather than in front of a player. Nothing else
    ///     in the knowledge base has that property.
    ///     </para>
    /// </remarks>
    public static class DerivedFacts
    {
        public static IReadOnlyList<Fact> Generate(GameData gameData)
        {
            var facts = new List<Fact>
            {
                Make("How many characters can I have?",
                     "Each account can have up to " + PlayerManager.MaxCharacterCount +
                     " characters. They share the account's coins and resources, but each has its own " +
                     "skills, inventory and clan rank.",
                     "characters", "account"),

                Make("What is the highest level?",
                     "Every skill goes up to level " + GameMath.MaxLevel + ".",
                     "skills", "levels"),

                Make("How long does a marketplace listing last?",
                     "A listing expires after " + (int)MarketplaceManager.ListingLifetime.TotalDays +
                     " days. When it expires the items go back to the seller; they are not lost.",
                     "marketplace", "trading"),

                Make("What does the vendor charge, and what does it pay?",
                     "The vendor pays a fixed price for an item. Buying one back costs at least " +
                     GameMath.VendorBuyMultiplier + " times what it pays, and never less than " +
                     GameMath.MinimumVendorBuyPrice + " coins, so selling something to the vendor and " +
                     "buying it straight back always loses money.",
                     "vendor", "trading", "coins"),

                Make("What does auto join cost?",
                     "Joining dungeons automatically costs " + PlayerManager.AutoJoinDungeonCost +
                     " coins, and raids " + PlayerManager.AutoJoinRaidCost + " coins.",
                     "coins", "dungeons", "raids"),

                Make("What does auto rest cost?",
                     "Resting automatically costs " + PlayerManager.AutoRestCostPerSecond +
                     " coins per second.",
                     "coins", "resting"),

                Make("Can I skip the enchanting cooldown?",
                     "Yes, for coins: " + PlayerManager.Enchanting_CooldownCoinsPerSecond +
                     " per second of cooldown remaining.",
                     "enchanting", "coins", "clan"),

                Make("How do experience multipliers work?",
                     "A multiplier goes up to " + SessionManager.MaxPlayerExpMultiplier + "x. Each scroll " +
                     "adds " + SessionManager.ExpMultiplierMinutesPerScroll + " minutes, and an event runs " +
                     "for up to " + SessionManager.ExpMultiplierLastTimeMinutes + " minutes.",
                     "experience", "multiplier", "scrolls"),
            };

            facts.AddRange(SkillList());
            facts.AddRange(ClanBankAllowances());

            var items = ItemCounts(gameData);
            if (items != null) facts.Add(items);

            return facts;
        }

        /// <summary>
        ///     The skills, from the list the character pages already walk by reflection.
        /// </summary>
        /// <remarks>
        ///     The best kind of derived fact: the fact is the query. A skill added to the game
        ///     appears here without anybody remembering to add it, because nothing here enumerates
        ///     them by hand.
        /// </remarks>
        private static IEnumerable<Fact> SkillList()
        {
            var names = new RavenNest.BusinessLogic.Extended.SkillsExtended()
                .AsList()
                .Select(x => x.Name)
                .ToList();

            if (names.Count == 0) yield break;

            yield return Make("What skills are there?",
                "There are " + names.Count + " skills: " + string.Join(", ", names) + ".",
                "skills");
        }

        /// <summary>
        ///     What each clan rank may take out of the clan bank by default.
        /// </summary>
        /// <remarks>
        ///     The defaults, not any one clan's settings, because a fact is shared by everybody who
        ///     asks and a clan's own limits are that clan's business. The assistant reads a specific
        ///     clan's allowance from a tool instead.
        /// </remarks>
        private static IEnumerable<Fact> ClanBankAllowances()
        {
            yield return Make("How much can I take out of the clan bank?",
                "By default it depends on your rank: Inactive and Recruit cannot withdraw at all, " +
                "Member can take " + ClanBankDefaults.ForRoleLevel(2) + " items a day, and Officer and above " +
                ClanBankDefaults.ForRoleLevel(3) + ". Anyone in the clan can deposit. The clan's owner is " +
                "never limited, and can change any of these. Soulbound items cannot go in at all.",
                "clan", "bank", "stash");
        }

        private static Fact ItemCounts(GameData gameData)
        {
            var items = gameData?.GetItems();
            if (items == null || items.Count == 0) return null;

            return Make("How many items are there in the game?",
                "There are " + items.Count + " items. You can search them on the items page, and the " +
                "assistant can look one up for you.",
                "items");
        }

        private static Fact Make(string title, string body, params string[] tags)
        {
            return new Fact
            {
                Title = title,
                Body = body,
                Tags = tags.ToList(),
                Source = FactSource.Derived,
                Status = FactStatus.Published
            };
        }
    }
}
