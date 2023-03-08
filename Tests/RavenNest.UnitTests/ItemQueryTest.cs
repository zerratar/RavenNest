using Microsoft.VisualStudio.TestTools.UnitTesting;
using RavenNest.BusinessLogic.ScriptParser;
using RavenNest.DataModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace RavenNest.UnitTests
{
    [TestClass]
    public class ItemQueryTest
    {
        [TestMethod]
        public void TestQuery_Sword_Returns_Sword()
        {
            var item = ParseQuery("abraxas");

        }

        private static TradeItem ParseQuery(string itemTradeQuery)
        {
            var itemManager = new ItemManager();
            var lexer = new Lexer();
            var tokens = lexer.Tokenize(itemTradeQuery, true);
            var index = tokens.Count - 1;

            var amount = 1L;
            var price = 0m;
            var modifiedQuery = "";
            var parseAmount = false;
            var parsePrice = false;

            while (true)
            {
                var token = tokens[index];
                if (token.Type == TokenType.Identifier)
                {
                    if (parsePrice && price <= 0 && TryParsePrice(token, out var p))
                    {
                        price = p;
                    }
                    else if (parseAmount && TryParseAmount(token, out var a))
                    {
                        amount = a;
                    }
                    else
                    {
                        modifiedQuery = token.Value + modifiedQuery;
                    }
                }
                else
                {
                    modifiedQuery = token.Value + modifiedQuery;
                }
                if (--index < 0) break;
            }

            var itemQuery = modifiedQuery.Trim();
            var item = itemManager.GetItems().FirstOrDefault(x => IsMatch(x.Name, itemQuery));
            if (item == null)
            {
                return null;
            }

            return new TradeItem(item, amount, price);
        }

        private static bool TryParsePrice(Token token, out decimal price)
        {
            price = 0m;

            var values = new Dictionary<string, decimal>
            {
                { "k", 1000 },
                { "m", 1000_000 },
                { "b", 1000_000_000 },
            };

            var lastChar = token.Value[token.Value.Length - 1];
            if (values.TryGetValue(char.ToLower(lastChar).ToString(), out var m))
            {
                if (decimal.TryParse(token.Value.Remove(token.Value.Length - 1), NumberStyles.Any, new NumberFormatInfo(), out var p))
                {
                    price = p * m;
                    return true;
                }
            }

            if (!char.IsDigit(lastChar)) return false;
            {
                if (!decimal.TryParse(token.Value, NumberStyles.Any, new NumberFormatInfo(), out var p)) return false;
                price = p;
                return true;
            }
        }

        private static bool TryParseAmount(Token token, out long amount)
        {
            if (long.TryParse(token.Value, out amount))
            {
                return true;
            }

            var values = new Dictionary<string, decimal>
            {
                { "k", 1000 },
                { "m", 1000_000 },
                { "b", 1000_000_000 },
            };

            if (token.Value.StartsWith("x", StringComparison.OrdinalIgnoreCase))
            {
                var lastChar = token.Value[token.Value.Length - 1];
                if (values.TryGetValue(char.ToLower(lastChar).ToString(), out var m))
                {
                    if (decimal.TryParse(token.Value.Remove(token.Value.Length - 1)[1..],
                        NumberStyles.Any, new NumberFormatInfo(), out var p))
                    {
                        amount = (long)(p * m);
                        return true;
                    }
                }

                if (long.TryParse(token.Value[1..], out amount))
                {
                    return true;
                }
            }
            else if (token.Value.EndsWith("x", StringComparison.OrdinalIgnoreCase))
            {
                if (long.TryParse(token.Value.Remove(token.Value.Length - 1), out amount))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsMatch(string name, string itemQuery)
        {
            if (name.Equals(itemQuery, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return GetItemNameAbbreviations(name)
                .Any(abbr => abbr.Equals(itemQuery, StringComparison.OrdinalIgnoreCase));
        }

        private static string[] GetItemNameAbbreviations(string name)
        {
            var nameList = new HashSet<string>(new List<string>
            {
                name.Trim(),
                name.Replace("-", "").Trim(),
                name.Replace("-", " ").Trim(),
                //name.Replace(" Sword", "").Trim(),
                name.Replace("-", " ").Trim(),
                name.Replace("Helmet", "Helm").Trim()
            }
            .Distinct());

            var tempList = nameList.ToArray();
            foreach (var item in tempList)
            {
                var items = item.Split(' ');
                var count = Math.Pow(2, items.Length);
                for (var i = 1; i <= count; ++i)
                {
                    var newList = items
                        .Skip(1)
                        .Where((t, j) => (i >> j) % 2 != 0)
                        .ToList();
                    newList.Insert(0, items.First());
                    if (newList.Count > 1)
                    {
                        nameList.Add(string.Join(" ", newList));
                    }
                }
            }

            var nameParts = name.Split(' ');
            var abbreviation = "";
            foreach (var part in nameParts)
            {
                if (part.Contains('-'))
                    abbreviation += string.Join("", part.Split('-').Select(x => x[0]));
                else
                    abbreviation += part[0];
            }

            if (abbreviation.Length >= 3)
            {
                nameList.Add(string.Join("", abbreviation.Take(3)));
            }

            return nameList.ToArray();
        }
    }
    public class ItemManager
    {
        public IReadOnlyList<Item> GetItems()
        {
            return new List<Item>
            {
new Item {Name = "Dragon 2H Staff"},
new Item {Name = "Adamantite Boots"},
new Item {Name = "Rune Chest"},
new Item {Name = "Abraxas Token"},
new Item {Name = "Abraxas 2H Staff"},
new Item {Name = "Bat Pet"},
new Item {Name = "Iron Boots"},
new Item {Name = "Iron Nugget"},
new Item {Name = "Abraxas Bow"},
new Item {Name = "Raven Pet"},
new Item {Name = "Mithril Leggings"},
new Item {Name = "Black Gloves"},
new Item {Name = "Mithril Boots"},
new Item {Name = "Mithril 2H Sword"},
new Item {Name = "Phantom Bow"},
new Item {Name = "Rune 2H Sword"},
new Item {Name = "Ultimate Tuna Slapper"},
new Item {Name = "Turd 2H Sword"},
new Item {Name = "Iron Chest"},
new Item {Name = "Rune Boots"},
new Item {Name = "Ghrims Stick"},
new Item {Name = "Phantom 2H Sword"},
new Item {Name = "Black Helmet"},
new Item {Name = "Phantom Helmet"},
new Item {Name = "Adamantite 2H Sword"},
new Item {Name = "Dragon 2H Sword"},
new Item {Name = "Ghost Pet"},
new Item {Name = "Black Leggings"},
new Item {Name = "Phantom Boots"},
new Item {Name = "Polar Bear Pet"},
new Item {Name = "Bronze Helmet"},
new Item {Name = "Green Orb Pet"},
new Item {Name = "Emerald Ring"},
new Item {Name = "Adamantite Chest"},
new Item {Name = "Iron Helmet"},
new Item {Name = "Abraxas Leggings"},
new Item {Name = "Iron Gloves"},
new Item {Name = "Abraxas Spirit"},
new Item {Name = "Black 2H Sword"},
new Item {Name = "Adamantite Bow"},
new Item {Name = "Black Boots"},
new Item {Name = "Iron Bow"},
new Item {Name = "Bronze Gloves"},
new Item {Name = "Phantom Leggings"},
new Item {Name = "Steel Helmet"},
new Item {Name = "Steel Leggings"},
new Item {Name = "Rune 2H Staff"},
new Item {Name = "Mithril Chest"},
new Item {Name = "Black Bow"},
new Item {Name = "Dragon Bow"},
new Item {Name = "Black 2H Staff"},
new Item {Name = "Ruby Ring"},
new Item {Name = "Bear Pet"},
new Item {Name = "Red Orb Pet"},
new Item {Name = "Abraxas Helmet"},
new Item {Name = "Mithril 2H Staff"},
new Item {Name = "Iron 2H Staff"},
new Item {Name = "Phantom Gloves"},
new Item {Name = "Wolf Pet"},
new Item {Name = "Bronze Boots"},
new Item {Name = "Mithril Nugget"},
new Item {Name = "Bronze Bow"},
new Item {Name = "Abraxas Gloves"},
new Item {Name = "Steel 2H Sword"},
new Item {Name = "Pickle Rick III"},
new Item {Name = "Bronze Leggings"},
new Item {Name = "Rune Nugget"},
new Item {Name = "Iron 2H Sword"},
new Item {Name = "Dragon Chest"},
new Item {Name = "Abraxas Chest"},
new Item {Name = "Steel 2H Staff"},
new Item {Name = "Blue Orb Pet"},
new Item {Name = "Adamantite Helmet"},
new Item {Name = "Steel Nugget"},
new Item {Name = "Dragon Gloves"},
new Item {Name = "Rune Leggings"},
new Item {Name = "Adamantite Gloves"},
new Item {Name = "Steel Gloves"},
new Item {Name = "Mithril Helmet"},
new Item {Name = "Steel Chest"},
new Item {Name = "Red Panda Pet"},
new Item {Name = "Bronze 2H Sword"},
new Item {Name = "Deer Pet"},
new Item {Name = "Phantom Chest"},
new Item {Name = "Fox Pet"},
new Item {Name = "Phantom Core"},
new Item {Name = "Pickle Rick"},
new Item {Name = "Abraxas Boots"},
new Item {Name = "Bacon 2H Sword"},
new Item {Name = "Black Chest"},
new Item {Name = "Iron Leggings"},
new Item {Name = "Rune Token"},
new Item {Name = "Mithril Gloves"},
new Item {Name = "Bronze Chest"},
new Item {Name = "Bronze 2H Staff"},
new Item {Name = "Mithril Bow"},
new Item {Name = "Dragon Boots"},
new Item {Name = "Dragon Helmet"},
new Item {Name = "Pickle Rick II"},
new Item {Name = "Ore Ingot"},
new Item {Name = "Rune Gloves"},
new Item {Name = "Adamantite Nugget"},
new Item {Name = "Rune Bow"},
new Item {Name = "Ruby Amulet"},
new Item {Name = "Steel Boots"},
new Item {Name = "Dragon Amulet"},
new Item {Name = "Bronze Sword"},
new Item {Name = "Rune Helmet"},
new Item {Name = "Steel Sword"},
new Item {Name = "Gold Ring"},
new Item {Name = "Adamantite Leggings"},
new Item {Name = "Phantom 2H Staff"},
new Item {Name = "Wood Plank"},
new Item {Name = "Steel Bow"},
new Item {Name = "Gold Amulet"},
new Item {Name = "Streamer Token"},
new Item {Name = "Gold Nugget"},
new Item {Name = "Sapphire"},
new Item {Name = "Abraxas 2H Sword"},
new Item {Name = "Dragon Leggings"},
new Item {Name = "Emerald"},
new Item {Name = "Ruby"},
new Item {Name = "Adamantite 2H Staff"},
new Item {Name = "Pumpkin Pet"},
new Item {Name = "Dragon Scale"}
            };
        }
    }
    public class TradeItem
    {
        public TradeItem(Item item, decimal amount, decimal pricePerItem)
        {
            Item = item;
            Amount = amount;
            PricePerItem = pricePerItem;
        }

        public Item Item { get; }
        public decimal Amount { get; }
        public decimal PricePerItem { get; }
    }
}
