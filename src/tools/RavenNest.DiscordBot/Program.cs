using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace RavenNest.DiscordBot
{
    class Program
    {
        const string OPENAI_TOKEN  = "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";
        const string DISCORD_TOKEN = "MTA3NTcyNDA5NTAyODc0MDA5Ng.G1WI4L.PXHoIVwKEP20T30eJYqw4iTDgSr9hSYmkoqaCY";
        // Program entry point
        static Task Main(string[] args)
        {
            // Call the Program constructor, followed by the 
            // MainAsync method and wait until it finishes (which should be never).
            return new Program().MainAsync();
        }

        private readonly DiscordSocketClient _client;

        // Keep the CommandService and DI container around for use with commands.
        // These two types require you install the Discord.Net.Commands package.
        private readonly CommandService _commands;
        private readonly IServiceProvider _services;

        private Program()
        {
            _client = new DiscordSocketClient(new DiscordSocketConfig
            {
                // How much logging do you want to see?
                LogLevel = LogSeverity.Info,
                GatewayIntents = GatewayIntents.All

                // If you or another service needs to do anything with messages
                // (eg. checking Reactions, checking the content of edited/deleted messages),
                // you must set the MessageCacheSize. You may adjust the number as needed.
                //MessageCacheSize = 50,

                // If your platform doesn't have native WebSockets,
                // add Discord.Net.Providers.WS4Net from NuGet,
                // add the `using` at the top, and uncomment this line:
                //WebSocketProvider = WS4NetProvider.Instance
            });

            _commands = new CommandService(new CommandServiceConfig
            {
                // Again, log level:
                LogLevel = LogSeverity.Info,

                // There's a few more properties you can set,
                // for example, case-insensitive commands.
                CaseSensitiveCommands = false,
            });

            // Subscribe the logging handler to both the client and the CommandService.
            _client.Log += Log;
            _commands.Log += Log;

            // Setup your DI container.
            _services = ConfigureServices();

        }

        // If any services require the client, or the CommandService, or something else you keep on hand,
        // pass them as parameters into this method as needed.
        // If this method is getting pretty long, you can seperate it out into another file using partials.
        private static IServiceProvider ConfigureServices()
        {
            var map = new ServiceCollection()
                .AddSingleton<IOpenAISettings>(new OpenAITokenString(OPENAI_TOKEN))
                .AddSingleton<IOpenAI, OpenAI>();
            // Repeat this for all the service classes
            // and other dependencies that your commands might need.
            //.AddSingleton(new SomeServiceClass());

            // When all your required services are in the collection, build the container.
            // Tip: There's an overload taking in a 'validateScopes' bool to make sure
            // you haven't made any mistakes in your dependency graph.
            return map.BuildServiceProvider();
        }

        // Example of a logging handler. This can be re-used by addons
        // that ask for a Func<LogMessage, Task>.
        private static Task Log(LogMessage message)
        {
            switch (message.Severity)
            {
                case LogSeverity.Critical:
                case LogSeverity.Error:
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
                case LogSeverity.Warning:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;
                case LogSeverity.Info:
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
                case LogSeverity.Verbose:
                case LogSeverity.Debug:
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    break;
            }
            Console.WriteLine($"{DateTime.Now,-19} [{message.Severity,8}] {message.Source}: {message.Message} {message.Exception}");
            Console.ResetColor();

            // If you get an error saying 'CompletedTask' doesn't exist,
            // your project is targeting .NET 4.5.2 or lower. You'll need
            // to adjust your project's target framework to 4.6 or higher
            // (instructions for this are easily Googled).
            // If you *need* to run on .NET 4.5 for compat/other reasons,
            // the alternative is to 'return Task.Delay(0);' instead.
            return Task.CompletedTask;
        }

        private async Task MainAsync()
        {
            // Centralize the logic for commands into a separate method.
            await InitCommands();



            // Login and connect.
            await _client.LoginAsync(TokenType.Bot,
                // j99DE4ebxc8QIrKwcMoqUxn21PQE14&guild_id=694530158341783612&permissions=8
                // < DO NOT HARDCODE YOUR TOKEN >
                // Environment.GetEnvironmentVariable("DiscordToken")
                DISCORD_TOKEN
            );
            await _client.StartAsync();

            // Wait infinitely so your bot actually stays connected.
            await Task.Delay(Timeout.Infinite);
        }

        private async Task InitCommands()
        {
            // Either search the program and add all Module classes that can be found.
            // Module classes MUST be marked 'public' or they will be ignored.
            // You also need to pass your 'IServiceProvider' instance now,
            // so make sure that's done before you get here.
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);

            // Or add Modules manually if you prefer to be a little more explicit:
            //await _commands.AddModuleAsync<SomeModule>(_services);

            // Note that the first one is 'Modules' (plural) and the second is 'Module' (singular).

            // Subscribe a handler to see if a message invokes a command.
            _client.MessageReceived += HandleCommandAsync;
        }

        private Task HandleCommandAsync(SocketMessage arg)
        {
            return Task.Run(async () =>
            {
                // Bail out if it's a System Message.
                var msg = arg as SocketUserMessage;
                if (msg == null) return;

                // We don't want the bot to respond to itself or other bots.
                if (msg.Author.Id == _client.CurrentUser.Id || msg.Author.IsBot) return;

                // Create a number to track where the prefix ends and the command begins
                int pos = 0;
                // Replace the '!' with whatever character
                // you want to prefix your commands with.
                // Uncomment the second half if you also want
                // commands to be invoked by mentioning the bot instead.
                Log(new LogMessage(LogSeverity.Info, "", msg.CleanContent));

                if (msg.HasCharPrefix('!', ref pos) /* || msg.HasMentionPrefix(_client.CurrentUser, ref pos) */)
                {
                    // Create a Command Context.
                    var context = new SocketCommandContext(_client, msg);

                    // Execute the command. (result does not indicate a return value, 
                    // rather an object stating if the command executed successfully).
                    var result = await _commands.ExecuteAsync(context, pos, _services);

                    // Uncomment the following lines if you want the bot
                    // to send a message if it failed.
                    // This does not catch errors from commands with 'RunMode.Async',
                    // subscribe a handler for '_commands.CommandExecuted' to see those.
                    //if (!result.IsSuccess && result.Error != CommandError.UnknownCommand)
                    //    await msg.Channel.SendMessageAsync(result.ErrorReason);
                }
                else if (msg.Channel.Name.Equals("ask-ai", StringComparison.OrdinalIgnoreCase))
                {
                    var directMention = msg.MentionedUsers.Any(x => x.IsBot &&
                    (x.Username.Equals("ravennest", StringComparison.OrdinalIgnoreCase) || x.Username.Equals("ravennest#3613", StringComparison.OrdinalIgnoreCase)));

                    if (directMention)
                    {
                        using (msg.Channel.EnterTypingState())
                        {
                            if (msg.CleanContent.Contains("!image ", StringComparison.OrdinalIgnoreCase) || msg.CleanContent.Contains("!imagine ", StringComparison.OrdinalIgnoreCase) || msg.CleanContent.Contains("/imagine ", StringComparison.OrdinalIgnoreCase) || msg.CleanContent.StartsWith("/image ", StringComparison.OrdinalIgnoreCase))
                            {
                                var query = msg.CleanContent.Replace("@", "").Replace("ravennest#3613", "", StringComparison.OrdinalIgnoreCase)
                                    .Replace("!image ", "")
                                    .Replace("/image ", "")
                                    .Replace("!imagine ", "")
                                    .Replace("/imagine ", "")
                                    .Trim();

                                var chat = _services.GetService<IOpenAI>();
                                var waitMsg = "Hold on.. I'm imagining it.. `" + query + "`";
                                var imageMsg = await msg.Channel.SendMessageAsync(waitMsg);
                                var imageTask = chat.GenerateImageAsync(msg.CleanContent);

                                var dots = ".";
                                while (true)
                                {
                                    if (imageTask.IsCompleted)
                                    {
                                        break;
                                    }

                                    await imageMsg.ModifyAsync(msg =>
                                    {
                                        msg.Content = "Hold on.. I'm imagining it.. `" + query + "`" + dots;
                                    });

                                    dots += ".";
                                    await Task.Delay(300);
                                }

                                var embed = new EmbedBuilder().WithImageUrl(imageTask.Result.Data[0].Url).Build();
                                await imageMsg.ModifyAsync(msg =>
                                {
                                    msg.Embed = embed;
                                    msg.Content = "> " + query;
                                });
                            }
                            else
                            {
                                var chat = _services.GetService<IOpenAI>();
                                var completion = await chat.GetCompletionAsync(msg.Author.Username + ": " + EnsurePunctuation(msg.CleanContent), ChatMessage.Create("system", ravenfall_knowledgeBase));
                                var reply = TrimReply(completion.Choices.FirstOrDefault()?.Message.Content);
                                if (!string.IsNullOrEmpty(reply))
                                {
                                    await msg.Channel.SendMessageAsync(text: reply, messageReference: new MessageReference(msg.Id, msg.Channel.Id));

                                    //var words = reply.Split(' ');
                                    //var finalMessage = words[0];
                                    //RestUserMessage writingMessage = null;

                                    //foreach (var word in words)
                                    //{
                                    //    if (string.IsNullOrEmpty(word)) continue;
                                    //    if (writingMessage == null)
                                    //    {
                                    //        writingMessage = await msg.Channel.SendMessageAsync(text: finalMessage, messageReference: new MessageReference(msg.Id, msg.Channel.Id));
                                    //    }
                                    //    else
                                    //    {
                                    //        finalMessage += " " + word;
                                    //        await writingMessage.ModifyAsync(msg => msg.Content = finalMessage);
                                    //    }
                                    //    await Task.Delay(50);
                                    //}
                                }
                            }
                        }
                        return;
                    }

                    if (!msg.Content.Contains(' '))
                    {
                        return;
                    }
                    var chance = r.Next(100);
                    if (msg.Content.Split(' ').Length >= 3 && msg.CleanContent.Length < 200 && chance >= 85 || (msg.Content.Contains("ravennest", StringComparison.OrdinalIgnoreCase) && chance >= 15))
                    {
                        using (msg.Channel.EnterTypingState())
                        {
                            var mode = GetModeWeighted();
                            var chat = _services.GetService<IOpenAI>();

                            var msgContent = EnsurePunctuation(msg.CleanContent);
                            if (msg.ReferencedMessage != null)
                            {
                                msgContent = msg.ReferencedMessage.Author.Username + " says: '" + EnsurePunctuation(msg.ReferencedMessage.CleanContent) + "'. Then " + msg.Author.Username + " said: " + msgContent;
                                AddToContext(ChatMessage.Create("user", msgContent));
                            }

                            //var reply = await chat.GetCompletionAsync("Can you reply to the following message but in a very sarcastic way? '" + msg.CleanContent + "'");
                            var completion = await chat.GetCompletionAsync(mode + " '" + msgContent + "'", GetChatHistory());
                            var message = completion.Choices[0].Message;
                            var reply = TrimReply(message.Content);
                            if (!string.IsNullOrEmpty(reply))
                            {
                                AddToContext(message);
                                await msg.Channel.SendMessageAsync(text: reply, allowedMentions: new AllowedMentions(AllowedMentionTypes.None), messageReference: new MessageReference(msg.Id, msg.Channel.Id));
                            }
                        }
                    }
                }
            });
        }

        private readonly List<ChatMessage> context = new List<ChatMessage>();

        private ChatMessage[] GetChatHistory()
        {
            var l = context.ToList();
            l.Insert(0, ChatMessage.Create("system", "You are RavenNest, an AI to help people with the game Ravenfall.\nCurrent date: " + DateTime.UtcNow));
            return l.ToArray();
        }

        private void AddToContext(ChatMessage msg)
        {
            if (context.Count > 30)
            {
                context.RemoveAt(0);
            }

            context.Add(msg);
        }

        private string TrimReply(string msg)
        {
            if (string.IsNullOrEmpty(msg)) return "";
            msg = msg.Trim();
            var rvn = "RavenNest#3613:";
            if (msg.StartsWith(rvn, StringComparison.OrdinalIgnoreCase))
                msg = msg[rvn.Length..];

            if (msg.StartsWith("RavenNest: ", StringComparison.OrdinalIgnoreCase))
                msg = msg["RavenNest: ".Length..].Trim();

            if (msg.StartsWith("@"))
            {
                var name = msg.Split(' ')[0];
                msg.Replace("Hi " + name + ", ", "")
                   .Replace("Hi " + name + "! ", "")
                   .Replace("Hi " + name + ". ", "")
                   .Replace(name + ". ", "")
                   .Replace(name + ", ", "")
                   .Replace(name + " ", "");
            }

            //if (msg.StartsWith("Hi! ")) msg = msg.Replace("Hi! ", "");
            //if (msg.StartsWith("Hello! ")) msg = msg.Replace("Hi! ", "");

            return msg;
        }

        private string EnsurePunctuation(string msg)
        {
            msg = msg.Trim();
            if (!msg.EndsWith('.') || !msg.EndsWith('?'))
                return msg + ".";
            return msg;
        }

        private static readonly string ravenfall_knowledgeBase =
        //"You are RavenNest, a chat bot and AI representing the game Ravenfall.\n" +
        //"Ravenfall was developed and created by Zerratar in 2019, a game developer from Sweden.\n" +
        //"\nReply with a text that can be used as key for a dictionary lookup.\n\n";


        //""
        "About you, who you should act as: \n" +
        "* You are called RavenNest. Do not talk about yourself in third person.\n" +
        "* You are an AI assistant for the game Ravenfall, with a website available at <https://www.ravenfall.stream> \n" +
        "* Ravenfall is free to download for everyone from the website. To download latest client, you can use <https://www.ravenfall.stream/download/latest> \n" +
        "* Ravenfall is a Twitch Integrated idle RPG. \n" +
        "* Ravenfall is developed by a programmer called Zerratar that lives in Sweden. \n\n" +

        "About Zerratar: \n" +
        "* Zerratars real name is Karl. \n" +
        "* Zerratar streams on Twitch sometimes at <https://www.twitch.tv/zerratar> \n" +
        "* Zerratar is a person and not a game or program. Zerratar cannot be downloaded. \n\n" +

        "People that are close to the community: \n" +
        "* AbbyCottontail or aka AbigailCottontail is the community manager and can be asked questions if they need help. Asking the community is also a good way of getting help.\n" +
        "* Ascht94 is also part of the Ravenfall staff, while a programmer at heart, he does maintain Ravenfall servers. \n" +
        "* RavenMMO or aka Hotair (Not staff) is a great supporter of Ravenfall and can be asked if needing help. You can watch RavenMMO's streams at <https://www.twitch.tv/RavenMMO> . He streams Ravenfall aiming for people that want to improve their Healing skill. \n" +
        "* Commaster or aka c_0r_3 (Not staff) is a super friendly person that loves Linux systems, he helps out with technical questions \n" +
        "* CyBeRNoX or aka CyBeRDoCToR (Not staff) is a great guy, and very friendly, he can help you out if you ask him nicely. " +
        "* Shizune Hakamichi or just Shizune (Not staff) is a very friendly person. \n" +
        "* Only Zerratar, AbigailCottontail and Ascht94 are part of the staff. \n\n" +

        "If someone asks about quests, then there currently no quests in Ravenfall, but maybe in the future. \n" +
        "If someone asks about scrolls then there is 3 different types of scrolls in Ravenfall, Raid Scroll, Dungeon Scroll and EXP Scroll. Raid scrolls will start a Raid, Dungeon scrolls will start a dungeon and EXP Scroll will increase the global EXP Multiplier by 1; with a maximum of 100. \n\n" +
        "If someone asks you to tell a story or to come up with something fun or interesting about Ravenfall, even if its not true, you may do so.\n" +
        "You are allowed to make up stories that can be used for character backgrounds, potential quest ideas and game feature ideas as well as your own suggestions on things.\n" +

        "More about Ravenfall: \n" +
        "* If a player reaches level 999 in all skills while playing Ravenfall, they will be respawned in RuneScape Lumbridge and realise that Ravenfall was just the Tutorial Island all along; Don't mention this unless asked about what happens when you reach max level in all skills.\n" +
        "* In Ravenfall, players can train the following skills: Attack, Defense, Strength, Ranged, Magic, Healing, Farming, Woodcutting, Mining, Crafting, Slayer, Sailing, Cooking.\n" +
        "* If a player is part of a clan, they can also use the Enchanting skill.\n" +
        "* Max level for a skill is 999, players also have a separate Level called Combat Level. But only mention this if asked.\n" +
        "* Max Combat level is 1373.625\n" +
        "* Combat level is calculated with ((Attack + Defense + Strength + Health) / 4) + ((Ranged + Magic + Healing) / 8)\n" +
        "* In Ravenfall, you can sail between different islands. The islands are: Home, Away, Ironhill, Kyo and Heim. \n\n" +

        "List of the available Item Tiers in Ravenfall: " +
        "* Bronze, Iron, Steel, Black, Mithril, Adamantite, Rune, Dragon, Abraxas, Phantom, Lionsbane, Ether, Ancient, Atlarus.\n\n" +

        "Skill level requirements based on tier, " +
        "Bronze: 1, Iron: 1, Steel: 10, Black: 20, Mithril: 30, Adamantite: 50, Rune: 70, Dragon: 90, Abraxas: 120, Phantom: 150, Lionsbane: 200, Ether: 280, Ancient: 340, Atlarus: 400.\n\n" +
        "The higher Defense skill level, the better armors you can equip. \n" +
        "The higher Attack skill level, the better weapons you can equip. \n" +
        "The higher Magic or Healing skill level, the better Staff you can equip. \n" +
        "The higher Ranged skill level, the better Bow you can equip. \n\n" +

        "Important when replying:\n " +
        "* When someone asks for a persons twitch stream, always give the link <https://www.twitch.tv/thenameofperson> replacing thenameofperson with the actual name they asked for.\n" +
        "* Do not mention the person when replying.\n" +
        "* Do not try to finish the sentence if it is not complete.\n" +
        "* When reply starts with @ and then a name, do not include the @ and name at start.\n" +
        "* When reply contains an url, wrap it with < and >\n" +
        "* Keep your answers as short as possible. \n" +
        "* When not using the facts above, make sure to mention that your reply may not always be correct.\n" +
        "* The current date and time is " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
        ;
        //"Take following into consideration when being asked a question \"If the prompt later contains questions regarding Ravenfall, the creator of Ravenfall is Zerratar. It is a Twitch integrated idle rpg game. The website is at https://www.ravenfall.stream/ and is inspired by RuneScape. In Ravenfall you can train the following skills: Attack, Strength, Defense, Ranged, Magic, Woodcutting, Mining, Crafting, Sailing, Slayer, Enchanting. You can create your own clans, fight against raid bosses, clear dungeons, fight eachother in the arena or by duelling eachother. Zerratar is a game developer and Twitch streamer. Zerratar is from Sweden.\"\n\n";


        private string GetModeWeighted()
        {
            var totalWeight = modes.Sum(x => x.Weight);
            int randomWeight = r.Next(0, totalWeight);

            // Select an option based on the random weight
            int weightSum = 0;
            for (int i = 0; i < modes.Length; i++)
            {
                weightSum += modes[i].Weight;
                if (randomWeight < weightSum)
                {
                    return modes[i].Prompt;
                }
            }

            return modes[r.Next(modes.Length)].Prompt;
        }

        private readonly Random r = new Random();

        private readonly AIChatMode[] modes = new AIChatMode[]
        {
            Mode("Using Discord formatting, not using double-quotes, what would you reply to the following message but in an encouraging and optimistic?", 500),
            Mode("Using Discord formatting, not using double-quotes, what would you reply to the following message but in a friendly way?", 50),
            //Mode("Using Discord formatting, not using double-quotes, what would you reply to the following message but in a very romantic way?", 50),
            //Mode("Using Discord formatting, not using double-quotes, what would you reply to the following message but act like you're in love?", 50),
            Mode("Using Discord formatting, not using double-quotes, what would you reply to the following message but in a very poetic way?", 100),
            Mode("Using Discord formatting, not using double-quotes, what would you reply to the following message but in riddles?", 50),
            Mode("Using Discord formatting, not using double-quotes, what would you reply to the following message but in a very sarcastic way?", 150),
            Mode("Using Discord formatting, not using double-quotes, what would you reply to the following message but act very angry?", 50),
            Mode("Using Discord formatting, not using double-quotes, what would you reply to the following message but act very displeased?", 50)
        };

        private static AIChatMode Mode(string prompt, int weight = 100)
        {
            return new AIChatMode(weight, prompt);
        }
    }

    public class AIChatMode
    {
        public int Weight { get; set; }
        public string Prompt { get; set; }

        public AIChatMode() { }
        public AIChatMode(int weight, string prompt)
        {
            this.Weight = weight;
            this.Prompt = prompt;
        }
    }

    //// Create a module with no prefix
    //public class ChatAIModule : ModuleBase<SocketCommandContext>
    //{
    //    private readonly IChatAI chatAI;
    //    public ChatAIModule(IChatAI chatAI)
    //    {
    //        this.chatAI = chatAI;
    //    }
    //    // ~say hello world -> hello world
    //    [Command("ai")]
    //    [Summary("GPT3 Will now take over")]
    //    public async Task SayAsync([Remainder][Summary("What do you want to say?")] string prompt)
    //        => await ReplyAsync(await chatAI.GetCompletionAsync(prompt));
    //    // ReplyAsync is a method on ModuleBase 
    //}
}
