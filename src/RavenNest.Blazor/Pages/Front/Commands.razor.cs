using Microsoft.AspNetCore.Components;
using RavenNest.BusinessLogic.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RavenNest.Blazor.Pages.Front
{
    public partial class Commands
    {
        private List<CommandDescriptor> commands = null;
        private List<CommandDescriptor> filtered = new();
        private List<KeyValuePair<string, int>> categories = new();

        /// <summary>
        ///     The file is read once at startup and the failure was swallowed, so a missing or
        ///     unreadable commands.json left the page on its loading spinner with nothing to say.
        /// </summary>
        private bool loadFailed;

        private string search = "";
        private string category;

        protected override void OnInitialized()
        {
            List<CommandDescriptor> loaded = null;
            try
            {
                var commandsPath = System.IO.Path.Combine(FolderPaths.GeneratedDataPath, "commands.json");
                if (System.IO.File.Exists(commandsPath))
                {
                    loaded = Newtonsoft.Json.JsonConvert.DeserializeObject<List<CommandDescriptor>>(
                        System.IO.File.ReadAllText(commandsPath));
                }
            }
            catch
            {
                loaded = null;
            }

            if (loaded == null || loaded.Count == 0)
            {
                loadFailed = true;
                return;
            }

            commands = FoldAliases(loaded);
            categories = commands
                .Where(x => !string.IsNullOrEmpty(x.Category))
                .GroupBy(x => x.Category)
                .OrderByDescending(x => x.Count())
                .ThenBy(x => x.Key)
                .Select(x => new KeyValuePair<string, int>(x.Key, x.Count()))
                .ToList();

            ApplyFilters();
        }

        /// <summary>
        ///     The file lists an alias as its own command, so !use, !eat, !drink and !consume were
        ///     four entries carrying the same description, and the page repeated it four times. An
        ///     alias whose description matches the command it points at is folded into it and shown
        ///     as another name for it; one whose description differs keeps its own entry, because
        ///     then it is a related command rather than a second name. !raidwar is the case that
        ///     matters: it points at !raid and does something else entirely.
        ///
        ///     Aliases chain, once: !coins points at !res, which points at !resources.
        /// </summary>
        private static List<CommandDescriptor> FoldAliases(List<CommandDescriptor> loaded)
        {
            var byName = new Dictionary<string, CommandDescriptor>(StringComparer.OrdinalIgnoreCase);
            foreach (var cmd in loaded)
            {
                if (!string.IsNullOrEmpty(cmd.Name))
                {
                    byName[cmd.Name] = cmd;
                }
            }

            var folded = new HashSet<CommandDescriptor>();

            foreach (var cmd in loaded)
            {
                var root = ResolveRoot(cmd, byName);
                if (root == null || ReferenceEquals(root, cmd))
                {
                    continue;
                }

                root.AlsoKnownAs.Add(cmd.Name);
                folded.Add(cmd);
            }

            var result = new List<CommandDescriptor>();
            foreach (var cmd in loaded)
            {
                if (folded.Contains(cmd))
                {
                    continue;
                }

                cmd.AlsoKnownAs.Sort(StringComparer.OrdinalIgnoreCase);

                // What is left with an alias set is a related command rather than a second name.
                if (!string.IsNullOrEmpty(cmd.Alias) && byName.ContainsKey(cmd.Alias))
                {
                    cmd.SeeAlso = cmd.Alias;
                }

                result.Add(cmd);
            }

            return result.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>
        ///     Follows the alias chain to the command this one is a second name for, or null when
        ///     it is not one. The visited set is there because nothing stops the file naming a
        ///     cycle, and a cycle would otherwise hang startup.
        /// </summary>
        private static CommandDescriptor ResolveRoot(
            CommandDescriptor cmd, Dictionary<string, CommandDescriptor> byName)
        {
            var visited = new HashSet<CommandDescriptor>();
            var current = cmd;

            while (current != null && visited.Add(current))
            {
                if (string.IsNullOrEmpty(current.Alias) ||
                    !byName.TryGetValue(current.Alias, out var target) ||
                    ReferenceEquals(target, current) ||
                    !SameDescription(current, target))
                {
                    return current;
                }

                current = target;
            }

            return current;
        }

        private static bool SameDescription(CommandDescriptor a, CommandDescriptor b)
        {
            return string.Equals(
                (a.Description ?? "").Trim(),
                (b.Description ?? "").Trim(),
                StringComparison.OrdinalIgnoreCase);
        }

        private void OnSearchChanged(string value)
        {
            search = value ?? "";
            ApplyFilters();
        }

        private void SelectCategory(string value)
        {
            category = value;
            ApplyFilters();
        }

        private void ClearFilters()
        {
            search = "";
            category = null;
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            filtered = commands.Where(Matches).ToList();
        }

        private bool Matches(CommandDescriptor cmd)
        {
            if (category != null && !string.Equals(cmd.Category, category, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(search))
            {
                return true;
            }

            // The leading ! is how everybody writes these, and typing it should not stop the search
            // from finding anything.
            var term = search.Trim().TrimStart('!');
            if (term.Length == 0)
            {
                return true;
            }

            return Contains(cmd.Name, term)
                || Contains(cmd.Description, term)
                || Contains(cmd.Category, term)
                || cmd.AlsoKnownAs.Any(x => Contains(x, term))
                || (cmd.Options != null && cmd.Options.Any(o => Contains(o.Name, term) || Contains(o.Description, term)));
        }

        private static bool Contains(string value, string term)
        {
            return value != null && value.Contains(term, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        ///     The command as you would type it: required arguments in angle brackets, optional
        ///     ones in round. Built as plain text so Razor escapes it, rather than the previous
        ///     version which wrote &amp;lt; into the string by hand and handed it to a MarkupString.
        /// </summary>
        public static string Signature(CommandDescriptor cmd)
        {
            var text = "!" + cmd.Name;
            if (cmd.Options == null || cmd.Options.Count == 0)
            {
                return text;
            }

            foreach (var option in cmd.Options)
            {
                text += option.IsRequired ? " <" + option.Name + ">" : " (" + option.Name + ")";
            }

            return text;
        }

        public class CommandDescriptor
        {
            public string Name { get; set; }
            /// <summary>
            ///   A description of the command, what it does, how to use it, etc.
            /// </summary>
            public string Description { get; set; }

            /// <summary>
            ///   Some commands have aliases, like !join and !j, this is referring to that other command that does the same as this one.
            /// </summary>
            public string Alias { get; set; }

            /// <summary>
            ///   In what category this command belongs, like 'training', 'moderation', 'minigames', etc.
            /// </summary>
            public string Category { get; set; }

            /// <summary>
            ///   Whether or not this command requires the user to be a broadcaster to use it.
            /// </summary>
            public bool RequiresBroadcaster { get; set; }

            /// <summary>
            ///   An example on how the command can be used, ex: '!join'
            /// </summary>
            public string UsageExample { get; set; }

            /// <summary>
            ///   The options for this command, like 'name', 'amount', 'item', etc.
            /// </summary>
            public List<CommandInputDescriptor> Options { get; set; }

            /// <summary>
            ///   The other names for this command, worked out by inverting Alias. Not in the file.
            /// </summary>
            [Newtonsoft.Json.JsonIgnore]
            public List<string> AlsoKnownAs { get; } = new();

            /// <summary>
            ///   A related command, for the entries whose Alias points at something that does a
            ///   different job. !raidwar is the one that does this.
            /// </summary>
            [Newtonsoft.Json.JsonIgnore]
            public string SeeAlso { get; set; }
        }

        public class CommandInputDescriptor
        {
            public string Name { get; set; }
            public CommandOptionType Type { get; set; }
            public string Description { get; set; }
            public bool IsRequired { get; set; }
            public List<CommandInputDescriptor> Options { get; set; }
            public List<string> Choices { get; set; }
            public CommandInputDescriptor() { }
            public CommandInputDescriptor(string name, CommandOptionType applicationCommandOptionType, string description, bool isRequired, List<CommandInputDescriptor> options, List<string> choices)
            {
                this.Name = name;
                this.Type = applicationCommandOptionType;
                this.Description = description;
                this.IsRequired = isRequired;
                this.Options = options != null && options.Count == 0 ? null : options;
                this.Choices = choices != null && choices.Count == 0 ? null : choices;
            }
        }

        public enum CommandOptionType : byte
        {
            SubCommand = 1,
            SubCommandGroup,
            String,
            Integer,
            Boolean,
            User,
            Channel,
            Role,
            Mentionable,
            Number,
            Attachment
        }
    }
}
