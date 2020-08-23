using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.ScriptParser;
using RavenNest.DataModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace RavenNest.BusinessLogic.Game
{
    public interface IItemResolver
    {
        IReadOnlyList<PlayerItem> Resolve(string query);
    }

    public class ItemResolver : IItemResolver
    {
        private readonly IGameData gameData;

        public ItemResolver(IGameData gameData)
        {
            this.gameData = gameData;
        }

        public IReadOnlyList<PlayerItem> Resolve(string query)
        {
            var itemOutput = new List<PlayerItem>();

            if (string.IsNullOrEmpty(query)) return itemOutput;
            query = query.Trim();

            var lines = query.Split('\n');
            foreach (var line in lines)
            {
                if (string.IsNullOrEmpty(line?.Trim()))
                    continue;

                var username = line.Split(' ')[0];
                Character player = gameData.GetCharacterByName(username);
                query = query.Substring(username.Length).Trim();

                if (string.IsNullOrEmpty(query)) return itemOutput;

                var itemQueries = query.Split(',');
                foreach (var itemQ in itemQueries)
                {
                    var itemQueryRaw = itemQ.Trim();
                    if (string.IsNullOrEmpty(itemQueryRaw))
                        continue;

                    var lexer = new Lexer();
                    var tokens = lexer.Tokenize(itemQueryRaw, true);
                    var index = tokens.Count - 1;

                    var amount = 1L;
                    var modifiedQuery = "";

                    while (true)
                    {
                        var token = tokens[index];
                        if (token.Type == TokenType.Identifier)
                        {
                            if (TryParseAmount(token, out var a))
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
                    var items = gameData.GetItems();
                    var item = items.FirstOrDefault(x => IsMatch(x.Name, itemQuery));
                    if (item == null)
                    {
                        continue;
                    }

                    itemOutput.Add(new PlayerItem(player, item, amount));
                }
            }
            return itemOutput;
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
                    if (decimal.TryParse(token.Value.Remove(token.Value.Length - 1).Substring(1), NumberStyles.Any, new NumberFormatInfo(), out var p))
                    {
                        amount = (long)(p * m);
                        return true;
                    }
                }

                if (long.TryParse(token.Value.Substring(1), out amount))
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
                name.Replace("-", " ").Replace(" Sword", "").Trim(),
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
                if (part.Contains("-"))
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

    public class PlayerItem
    {
        public PlayerItem(Character character, Item item, decimal amount)
        {
            Item = item;
            Amount = amount;
            Character = character;
        }

        public Item Item { get; }
        public decimal Amount { get; }
        public Character Character { get; }
    }

}