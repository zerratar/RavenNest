using System;
using System.Collections.Generic;
using System.Linq;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    /// <summary>
    /// Experience books (v0.9.7): what a quest gives in place of experience for a skill that is at the highest level
    /// already. A book belongs to a category of skills, and its reader picks which skill of the category gets it
    /// (!use gathering book fishing). It never carries an amount of experience (a level 999 skill's reward would send
    /// a low skill up dozens of levels): it carries a share of a level, and gives that share of the next level of the
    /// skill it is read for, as the quests' own experience rewards are worked out.
    /// </summary>
    /// <remarks>
    /// Three sizes, each one a quest's whole reward: a small book is a daily quest's share of a level, a medium one a
    /// finished chain's and a large one a weekly quest's, so a quest pays one book and not a stack. The items are made at startup
    /// (GameData.EnsureExperienceBooks), bound to their owner and worth nothing at the vendor. The game has the same
    /// categories (ExperienceBooks in its QuestCommand.cs) to word its replies; the server decides.
    /// </remarks>
    public static class ExperienceBooks
    {
        /// <summary>A small book: the share of a level a daily quest pays.</summary>
        public const double SmallShare = 0.15;

        /// <summary>A medium book: the share of a level a finished quest chain pays.</summary>
        public const double MediumShare = 0.5;

        /// <summary>A large book: the share of a level a weekly quest pays (six dailies).</summary>
        public const double LargeShare = 0.9;

        public const int Small = 0, Medium = 1, Large = 2;

        /// <summary>The sizes, smallest first: the word in the item's name and the share of a level.</summary>
        public static readonly (string Name, double Share)[] Sizes = { ("Small", SmallShare), ("Medium", MediumShare), ("Large", LargeShare) };

        /// <summary>The most books one !use reads.</summary>
        public const int MaxPerUse = 100;

        public sealed class Category
        {
            /// <summary>"gathering": what players type.</summary>
            public string Key;

            /// <summary>"Gathering": the word in the item's name.</summary>
            public string Name;
            public Skill[] Skills;
        }

        public sealed class Book
        {
            public Category Category;

            /// <summary><see cref="Small"/>, <see cref="Medium"/> or <see cref="Large"/>.</summary>
            public int Size;

            /// <summary>The item's name: "Small Gathering Book".</summary>
            public string Name;
            public double Share;
        }

        // Every skill is in exactly one category. Health is the skill players see as Vitality.
        public static readonly Category[] Categories =
        {
            new Category { Key = "combat", Name = "Combat", Skills = new[] { Skill.Attack, Skill.Defense, Skill.Strength, Skill.Health, Skill.Ranged, Skill.Magic, Skill.Healing } },
            new Category { Key = "gathering", Name = "Gathering", Skills = new[] { Skill.Woodcutting, Skill.Mining, Skill.Fishing, Skill.Farming, Skill.Gathering } },
            new Category { Key = "artisan", Name = "Artisan", Skills = new[] { Skill.Crafting, Skill.Cooking, Skill.Alchemy } },
            new Category { Key = "adventure", Name = "Adventure", Skills = new[] { Skill.Slayer, Skill.Sailing, Skill.Taming } },
        };

        public static readonly Book[] All = Categories.SelectMany(c => Sizes.Select((size, i) =>
            new Book { Category = c, Size = i, Name = size.Name + " " + c.Name + " Book", Share = size.Share })).ToArray();

        /// <summary>The book an item is, by the item's name, or null.</summary>
        public static Book Find(string itemName) =>
            itemName == null ? null : All.FirstOrDefault(b => string.Equals(b.Name, itemName, StringComparison.OrdinalIgnoreCase));

        public static Book Get(Category category, int size) => All.First(b => b.Category == category && b.Size == size);

        /// <summary>The category a skill's books come from, or null for something that is no skill.</summary>
        public static Category CategoryOf(int skillIndex) => Categories.FirstOrDefault(c => c.Skills.Any(s => (int)s == skillIndex));

        /// <summary>
        /// The books closest to a share of a level, how many of each size (indexed by <see cref="Small"/>,
        /// <see cref="Medium"/>, <see cref="Large"/>): a daily's 15% is one small book, a chain's 50% one medium one, a
        /// weekly's 90% one large one, a grand chain's 100% two medium ones. What does not come out even goes to the
        /// nearest books, a half up (a seasonal quest's 22.5% is two small ones); a share under half a small book is
        /// no book.
        /// </summary>
        public static int[] For(double share)
        {
            var best = new int[3];
            if (share <= 0 || double.IsNaN(share) || double.IsInfinity(share)) return best;
            var bestError = double.MaxValue;
            var mostLarge = (int)Math.Floor(share / LargeShare + 1e-9);
            for (var large = 0; large <= mostLarge; ++large)
            {
                var afterLarge = Math.Max(0, share - large * LargeShare);
                var mostMedium = (int)Math.Floor(afterLarge / MediumShare + 1e-9);
                for (var medium = 0; medium <= mostMedium; ++medium)
                {
                    var rest = Math.Max(0, afterLarge - medium * MediumShare);
                    var small = (int)Math.Floor(rest / SmallShare + 0.5 + 1e-9);
                    var error = Math.Abs(large * LargeShare + medium * MediumShare + small * SmallShare - share);
                    // as close, in fewer books: the bigger ones
                    if (error < bestError - 1e-9 || (Math.Abs(error - bestError) <= 1e-9 && large + medium + small < best.Sum()))
                    {
                        best = new[] { small, medium, large };
                        bestError = error;
                    }
                }
            }
            return best;
        }

        /// <summary>Whether a count of books (<see cref="For"/>) has any.</summary>
        public static bool Any(int[] books) => books != null && books.Any(b => b > 0);

        /// <summary>
        /// The books as the game says them after "and a", the biggest first: "Small Gathering Book", "stack of 2 Medium
        /// Gathering Books", "Large Combat Book and a Small Combat Book". Empty for none.
        /// </summary>
        public static string Describe(Category category, int[] books)
        {
            var parts = new List<string>();
            if (books == null) return string.Empty;
            for (var size = Math.Min(books.Length, Sizes.Length) - 1; size >= 0; --size)
            {
                if (books[size] > 0) parts.Add(Counted(Get(category, size).Name, books[size]));
            }
            return string.Join(" and a ", parts);
        }

        private static string Counted(string name, int count) => count == 1 ? name : "stack of " + count + " " + name + "s";

        /// <summary>"Woodcutting, Mining, Fishing, Farming or Gathering", as players see the skills.</summary>
        public static string SkillNames(Category category)
        {
            var names = category.Skills.Select(SkillName).ToArray();
            return names.Length == 1 ? names[0] : string.Join(", ", names, 0, names.Length - 1) + " or " + names[names.Length - 1];
        }

        public static string SkillName(Skill skill) => skill == Skill.Health ? "Vitality" : skill.ToString();

        /// <summary>
        /// What the game sends with a book's !use: the skill ("Fishing", or "Vitality" for Health) and, after a dash, how
        /// many to read ("Fishing-3"); one without a number.
        /// </summary>
        public static bool TryParseUse(string arg, out Skill skill, out int count)
        {
            skill = Skill.None;
            count = 1;
            if (string.IsNullOrWhiteSpace(arg)) return false;
            var text = arg.Trim();
            var dash = text.LastIndexOf('-');
            if (dash > 0)
            {
                if (!int.TryParse(text.Substring(dash + 1), out count) || count < 1) return false;
                text = text.Substring(0, dash);
            }
            if (string.Equals(text, "Vitality", StringComparison.OrdinalIgnoreCase)) text = nameof(Skill.Health);
            // a name only: Enum.TryParse would take "3" for the fourth skill
            if (text.Length == 0 || !text.All(char.IsLetter)) return false;
            return Enum.TryParse(text, true, out skill) && CategoryOf((int)skill) != null;
        }
    }
}
