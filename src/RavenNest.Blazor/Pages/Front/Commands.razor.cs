using Microsoft.AspNetCore.Components;
using RavenNest.BusinessLogic.Data;
using System.Collections.Generic;
using System.Linq;

namespace RavenNest.Blazor.Pages.Front
{
    public partial class Commands
    {
        private List<CommandDescriptor> commands = null;
        protected override void OnInitialized()
        {
            try
            {
                var commandsPath = System.IO.Path.Combine(FolderPaths.GeneratedDataPath, "commands.json");
                if (System.IO.File.Exists(commandsPath))
                {
                    this.commands = Newtonsoft.Json.JsonConvert.DeserializeObject<List<CommandDescriptor>>(System.IO.File.ReadAllText(commandsPath));
                }
            }
            catch
            {
            }
        }

        public string GetCommandFormat(CommandDescriptor cmd)
        {
            return ("!" + cmd.Name + " " + GetCommandArguments(cmd)).Trim();
        }

        private static string GetCommandArguments(CommandDescriptor cmd)
        {
            var str = "";
            if (cmd.Options != null)
            {
                var args = new List<string>();
                foreach (var opt in cmd.Options)
                {
                    args.Add(opt.IsRequired ? ("&lt;required: " + opt.Name + "&gt;") : ("(optional: " + opt.Name + ")"));
                }
                return string.Join(" ", args);
            }

            return str;
        }

        public MarkupString GetCommandArgumentsMarkup(CommandDescriptor cmd)
        {
            return new MarkupString($"{GetCommandArguments(cmd)}");
        }

        public MarkupString GetCommandFormatMarkup(CommandDescriptor cmd)
        {
            return new MarkupString($"<pre><code>{GetCommandFormat(cmd)}</pre></code>");
        }

        public MarkupString GetUsageExampleMarkup(CommandDescriptor cmd)
        {
            return new MarkupString($"<blockquote>{cmd.UsageExample}</blockquote>");
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
