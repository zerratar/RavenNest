using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using RavenNest.BusinessLogic;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Extended;
using RavenNest.BusinessLogic.Extensions;
using RavenNest.BusinessLogic.Game;
using RavenNest.BusinessLogic.OpenAI.Conversations;
using RavenNest.DataModels;
using RavenNest.Models;
using Shinobytes.OpenAI;
using Shinobytes.OpenAI.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Services
{
    public class AIAssistanceFunctionCallbacks
        : ReflectionBasedOpenAIFunctionCallService<AIAssistanceFunctionCallbacks>
    {
        private readonly GameData gameData;
        private readonly IServerManager serverManager;
        private readonly ItemManager itemManager;

        public AIAssistanceFunctionCallbacks(
            GameData gameData,
            IServerManager serverManager,
            ItemManager itemManager)
            : base()
        {
            this.gameData = gameData;
            this.serverManager = serverManager;
            this.itemManager = itemManager;
        }

        [Description("Gets active amount of game sessions, how many twitch streamers that are currently running ravenfall")]
        public int GetActiveSessionsCount()
        {
            return gameData.GetActiveSessions().Count;
        }

        [Description("Gets details about the user using a provided guid user id.")]
        public User GetUserById(Guid userId)
        {
            return gameData.GetUser(userId);
        }

        [Description("Gets all the available items in the game.")]
        public IReadOnlyList<DataModels.Item> GetAllGameItems()
        {
            return gameData.GetItems();
        }
        /*
        [Description("Gets a list of character Ids owned by the specified user. ")]
        public IReadOnlyList<DataModels.Character> GetCharactersByUserId(Guid userId)
        {
            return gameData.GetCharactersByUserId(userId);
        }

        [Description("Gets all inventory items/items owned by a character, both equipped and in inventory using the provided character id.")]
        public IReadOnlyList<DataModels.InventoryItem> GetInventoryItemsByCharacterId(Guid characterId)
        {
            return gameData.GetInventoryItems(characterId);
        }

        [Description("Gets the equipped items by a character using the provided character id.")]
        public IReadOnlyList<DataModels.InventoryItem> GetEquippedInventoryItemsByCharacterId(Guid characterId)
        {
            return gameData.GetEquippedItems(characterId);
        }

        [Description("Gets character info only, no skills/stats, no inventory, only name, identifier, userIdLock (which stream the character is currently in) using a provided guid character id.")]
        public DataModels.Character GetCharacter(Guid characterId)
        {
            return gameData.GetCharacter(characterId);
        }

        [Description("Gets the skills/stats of a character using the provided character id.")]
        public DataModels.Skills GetSkillsByCharacterId(Guid characterId)
        {
            var character = gameData.GetCharacter(characterId);
            var skills = gameData.GetCharacterSkills(character.SkillsId);
            return skills;
        }

        [Description("Gets character and the skills/stats available for the character both experience and level wise, using a provided guid character id.")]
        public Tuple<DataModels.Character, DataModels.Skills> GetCharacterAndSkills(Guid characterId)
        {
            var character = gameData.GetCharacter(characterId);
            var skills = gameData.GetCharacterSkills(character.SkillsId);
            return Tuple.Create(character, skills);
        }

        [Description("Gets the combat level of a character using the provided character id.")]
        public int GetCharacterCombatLevel(Guid characterId)
        {
            var character = gameData.GetCharacter(characterId);
            var skills = gameData.GetCharacterSkills(character.SkillsId);
            if (skills == null) return 3;
            return (int)(((skills.AttackLevel + skills.DefenseLevel + skills.HealthLevel + skills.StrengthLevel) / 4f) + ((skills.RangedLevel + skills.MagicLevel + skills.HealingLevel) / 8f));
        }
        */
        /*

        [Description("Adds X amount of coins to the player of given userId")]
        public double AddCoins(Guid userId, long coins)
        {
            var resources = gameData.GetResources(userId);
            if (resources == null) return 0;
            resources.Coins += coins;
            return resources.Coins;
        }

        [Description("Sets the exact amount of coins owned by a user given userId")]
        public double SetCoins(Guid userId, long coins)
        {
            var resources = gameData.GetResources(userId);
            if (resources == null) return 0;
            resources.Coins = coins;
            return resources.Coins;
        }
        */


        [Description("Creates and add a new item that can be found by Mining. (Items related to mining only, eg. Copper Ore, Tin, Coal, Iron Ore, Gold Nugget, etc) along with a required mining level (Max 999), a droprate 0..1 and sell price")]
        public RavenNest.Models.Item AddMiningDrop(string name, int requiredMiningLevel, double dropRate, long sellPrice)
        {
            var item = new RavenNest.Models.Item()
            {
                Id = Guid.NewGuid(),
                Category = RavenNest.Models.ItemCategory.Resource,
                Type = RavenNest.Models.ItemType.Mining,
                Name = name,
                ShopSellPrice = sellPrice,
            };

            if (itemManager.TryAddItem(item))
            {
                TryGetSkillIndex("Mining", out var fishingIndex);

                var dropToAdd = new RavenNest.DataModels.ResourceItemDrop
                {
                    ItemId = item.Id,
                    DropChance = dropRate,
                    Id = Guid.NewGuid(),
                    ItemName = name,
                    LevelRequirement = requiredMiningLevel,
                    Skill = fishingIndex
                };
                gameData.Add(dropToAdd);

                return item;
            }

            return null;
        }

        [Description("Creates and add a new item that can be found by Woodcutting. (Items related to woodcutting only, eg. Regular Logs, Oak Logs, Pine Logs, Cursed Willow Logs, etc) along with a required woodcutting level (Max 999), a droprate 0..1 and sell price")]
        public RavenNest.Models.Item AddWoodcuttingDrop(string name, int requiredWoodcuttingLevel, double dropRate, long sellPrice)
        {
            var item = new RavenNest.Models.Item()
            {
                Id = Guid.NewGuid(),
                Category = RavenNest.Models.ItemCategory.Resource,
                Type = RavenNest.Models.ItemType.Woodcutting,
                Name = name,
                ShopSellPrice = sellPrice,
            };

            if (itemManager.TryAddItem(item))
            {
                TryGetSkillIndex("Woodcutting", out var fishingIndex);

                var dropToAdd = new RavenNest.DataModels.ResourceItemDrop
                {
                    ItemId = item.Id,
                    DropChance = dropRate,
                    Id = Guid.NewGuid(),
                    ItemName = name,
                    LevelRequirement = requiredWoodcuttingLevel,
                    Skill = fishingIndex
                };
                gameData.Add(dropToAdd);

                return item;
            }

            return null;
        }

        [Description("Creates and add a new item that can be found by farming. (Items related to farming only, eg. Wheat, Apple, Carrot, Onion, etc) along with a required farming level (Max 999), a droprate 0..1 and sell price")]
        public RavenNest.Models.Item AddFarmingDrop(string name, int requiredFarmingLevel, double dropRate, long sellPrice)
        {
            var item = new RavenNest.Models.Item()
            {
                Id = Guid.NewGuid(),
                Category = RavenNest.Models.ItemCategory.Resource,
                Type = RavenNest.Models.ItemType.Farming,
                Name = name,
                ShopSellPrice = sellPrice,
            };

            if (itemManager.TryAddItem(item))
            {
                TryGetSkillIndex("Farming", out var fishingIndex);

                var dropToAdd = new RavenNest.DataModels.ResourceItemDrop
                {
                    ItemId = item.Id,
                    DropChance = dropRate,
                    Id = Guid.NewGuid(),
                    ItemName = name,
                    LevelRequirement = requiredFarmingLevel,
                    Skill = fishingIndex,
                };
                gameData.Add(dropToAdd);

                return item;
            }

            return null;
        }

        [Description("Creates and add a new item that can be found by fishing. (Fish only) along with a required fishing level (Max 999), a droprate 0..1 and sell price")]
        public RavenNest.Models.Item AddFishingDrop(string name, int requiredFishingLevel, double dropRate, long sellPrice)
        {
            var item = new RavenNest.Models.Item()
            {
                Id = Guid.NewGuid(),
                Category = RavenNest.Models.ItemCategory.Resource,
                Type = RavenNest.Models.ItemType.Fishing,
                Name = name,
                ShopSellPrice = sellPrice,
            };

            if (itemManager.TryAddItem(item))
            {
                TryGetSkillIndex("Fishing", out var fishingIndex);

                var dropToAdd = new RavenNest.DataModels.ResourceItemDrop
                {
                    ItemId = item.Id,
                    DropChance = dropRate,
                    Id = Guid.NewGuid(),
                    ItemName = name,
                    LevelRequirement = requiredFishingLevel,
                    Skill = fishingIndex
                };
                gameData.Add(dropToAdd);

                return item;
            }

            return null;
        }

        private bool TryGetSkillIndex(string Skill, out int index)
        {
            index = -1;
            if (!string.IsNullOrEmpty(Skill))
            {
                if (int.TryParse(Skill, out var si))
                {
                    index = si;
                }
                else
                {
                    // try get index by name.
                    var i = RavenNest.DataModels.Skills.IndexOf(Skill);
                    if (i != -1)
                        index = i;
                }
            }
            return index != -1;
        }

        [Description("Gets the current Server Time in UTC.")]
        public DateTime GetServerTimeUtc()
        {
            return DateTime.UtcNow;
        }

        [Description("Create and set a Exp Multiplier event with multiplier value, title, starting time, ending time. Start time can be null and will start immediately if so.")]
        public ExpMultiplierEvent SetExpMultiplier(int multiplier, string title, DateTime? startTime, DateTime endTime)
        {
            return serverManager.SendExpMultiplierEventAsync(multiplier, title, startTime, endTime);
        }
    }

    public class AIAssistanceService : RavenNestService
    {
        private static string KnowledgeBase =>
            "You are an AI Assistant for Administrators of the Twitch Idle RPG game Ravenfall. You will take upon any request and try to help out any way you can. You are able to do administrative actions that directly interacts with the backend, gameserver, APIs and website. Use provided functions when necessary.\n" +
            "Whenever asked for doing an action or calling a function that may have a side effect such as writes to a database, changes to a character, items, stats, game session, exp multipliers, etc. These actions must always be confirmed by the user before calling those functions, when confirming display what the expected changes is or what cvalues are expected to be used in the function call.\n" +
            $"Current Server Time (Utc): {DateTime.UtcNow:F}\n";

        private readonly GameData gameData;
        private readonly IOpenAIClient openAI;
        private readonly IOpenAIFunctionCallService functionCallbacks;
        private readonly AIConversationManager conversations;
        private readonly Function[] functions;

        public AIAssistanceService(
            GameData gameData,
            IOpenAIClient openAIClient,
            IHttpContextAccessor accessor,
            IOpenAIFunctionCallService functionCallbacks,
            SessionInfoProvider sessionInfoProvider)
            : base(accessor, sessionInfoProvider)
        {
            this.gameData = gameData;
            this.openAI = openAIClient;
            this.functionCallbacks = functionCallbacks;
            this.conversations = new AIConversationManager(gameData);
            var functions = new List<Function>()
            {
                Function.Create(ClearCurrentConversation, this, preventDefault: true),
                Function.Create(GetSession, this),
            };
            functions.AddRange(functionCallbacks.GetFunctions());
            this.functions = functions.ToArray();
        }
        public bool ShowFunctionCallResults { get; set; } = false;

        public MarkupString FormatMessage(AIConversationMessage message)
        {
            if (message.Message.Role == MessageRole.Function)
            {
                return new MarkupString(Markdig.Markdown.ToHtml("```json\n" + message.Message.Content + "\n```"));
            }

            return new MarkupString(Markdig.Markdown.ToHtml(message.Message.Content));
        }

        [Description("Remove all conversations for the current user.")]
        public bool RemoveAllConversations()
        {
            var session = GetSession();
            if (!IsValidSession(session)) return false;

            var uid = session.UserId;
            return conversations.RemoveAll(uid);
        }

        [Description("Clears the current conversation's chat history with the AI Assistant.")]
        public bool ClearCurrentConversation()
        {
            var session = GetSession();
            if (!IsValidSession(session)) return false;
            conversations.ClearCurrentConversation(session.UserId);
            return true;
        }

        public AIConversation ClearConversationHistory(Guid conversationId)
        {
            var session = GetSession();
            if (!IsValidSession(session)) return null;

            var uid = session.UserId;
            var conversation = conversations.Get(conversationId);
            if (conversation == null) return null;
            if (conversation.UserId != uid) return null;
            return conversations.ClearConversation(conversation);
        }

        public AIConversation GetLastConversion()
        {
            var session = GetSession();
            if (!IsValidSession(session)) return null;

            return conversations.GetLatestOrCreate(session.UserId);
        }

        public AIConversation AddMessage(string input)
        {
            var session = GetSession();
            if (!IsValidSession(session)) return null;

            var uid = session.UserId;
            var conversation = conversations.GetLatestOrCreate(uid);
            conversation.Add(input, MessageRole.User);
            return conversation;
        }

        public Task<AIConversation> SendMessageAsync(string input, bool useGPT4)
        {
            var session = GetSession();
            if (!IsValidSession(session)) return null;

            var uid = session.UserId;

            var conversation = conversations.GetLatestOrCreate(uid);
            var prompt = conversation.GetMessageByContent(input);

            // if we don't have a prompt message, generate one here. this will be better as we will then have a reference
            // to the same prompt later.
            if (prompt == null)
            {
                prompt = conversation.Add(input, MessageRole.User);
            }

            return SendConversationAsync(conversation, prompt, useGPT4);
        }

        public Task<AIConversation> SendConversationAsync(AIConversation conversation, bool useGPT4)
        {
            var prompt = conversation.GetLastMessage();
            return SendConversationAsync(conversation, prompt, useGPT4);
        }

        public async Task<AIConversation> SendConversationAsync(AIConversation conversation, AIConversationMessage prompt, bool useGPT4)
        {
            var session = GetSession();
            if (!IsValidSession(session)) return null;

            var builder = openAI.GetRequestBuilder();
            var request = builder
                .SetKnowledgeBase(GetKnowledgeBase())
                .AddFunctions(functions)
                .AddMessages(Transform(conversation.GetMessages()))
                .Build(useGPT4 ? OpenAIModelSelection.GPT4 : OpenAIModelSelection.GPT3_5);

            var result = await openAI.GetCompletionAsync(request, System.Threading.CancellationToken.None);
            var choice = result.Choices.FirstOrDefault();

            var response = conversation.Add(choice.Message);

            switch (choice.FinishReason)
            {
                case "function_call":
                    return await HandleFunctionCallAsync(session, conversation, prompt, response, useGPT4);
                default:
                    return await HandleMessageResponseAsync(session, conversation, prompt, response, useGPT4);
            }
        }

        private string GetKnowledgeBase()
        {
            var session = GetSession();

            var str = KnowledgeBase + "\n";
            str += "Details about the currently logged in user:\n";
            str += $"User Id: {session.UserId}\n";
            str += $"UserName: {session.UserName}\n";
            str += $"Patreon Tier: {session.Tier}\n";
            str += $"Twitch User Id: {session.TwitchUserId}\n";
            str += $"Administrator: {session.Administrator}\n";
            str += $"Moderator: {session.Moderator}\n";

            var user = gameData.GetUser(session.UserId);
            var resources = gameData.GetResources(user);

            str += $"Available Resources in game, Coins: {resources.Coins}, Wood: {resources.Wood}, Ore: {resources.Ore}, Fish: {resources.Fish}, Wheat: {resources.Wheat}\n";

            var characters = gameData.GetCharactersByUserId(session.UserId);
            str += $"Character Count: {characters.Count}\n";
            str += $"Characters:\n";
            foreach (var c in characters)
            {
                var skills = gameData.GetCharacterSkills(c.SkillsId);
                var combatLevel = 3;
                if (skills != null)
                    combatLevel = (int)(((skills.AttackLevel + skills.DefenseLevel + skills.HealthLevel + skills.StrengthLevel) / 4f) + ((skills.RangedLevel + skills.MagicLevel + skills.HealingLevel) / 8f));

                var sessionInfo = ModelMapper.GetCharacterSessionInfo(gameData, c);
                var statusString = GetCharacterStatus(c);
                str += $" * ID: {c.Id}, Name: {c.Name}, Unique Name: {c.Name}#{c.CharacterIndex}, Combat Level: {combatLevel}, Exp Last Updated: {GetLastUpdateString(sessionInfo.SkillsUpdated)}, {statusString}\n";
            }

            return str;
        }

        private string GetCharacterStatus(Character character)
        {
            var str = "";
            var state = gameData.GetCharacterState(character.StateId);
            var trainingSkill = !string.IsNullOrEmpty(state.TaskArgument) ? state.TaskArgument : state.Task;
            if (state != null && state.InOnsen.GetValueOrDefault())
            {
                str += "Currently resting and have " + GetRestedTime(state.RestedTime) + " of rested time ";
            }
            else if (!string.IsNullOrEmpty(trainingSkill))
            {
                str += "Currently training ";
                str += trainingSkill;//GetTrainingSkillName(character);
                str += " ";
            }
            else
            {
                str += "Currently ";
            }

            if (state != null)
            {
                if (state.InDungeon.GetValueOrDefault())
                {
                    str += "in the dungeon";
                }
                else if (state.InArena)
                {
                    str += "in the Arena";
                }
                else if (state.InRaid)
                {
                    str += "in a Raid";
                }
                else if (!string.IsNullOrEmpty(state.Island))
                {
                    str += " at ";
                    str += state.Island;
                }
                else
                {
                    str += "sailing";
                }
            }

            return str;
        }

        private string GetRestedTime(double? restedTime)
        {
            return FormatTime(System.TimeSpan.FromSeconds(restedTime ?? 0));
        }
        public static string FormatTime(TimeSpan time)
        {
            if (time.TotalSeconds < 60) return time.TotalSeconds + " seconds";
            if (time.TotalMinutes < 60)
                return (int)Math.Floor(time.TotalMinutes) + " minutes";

            return $"{time.Hours} hours, {time.Minutes} minutes";
        }


        public string GetLastUpdateString(DateTime update)
        {
            var elapsed = DateTime.UtcNow - update;
            if (update == DateTime.MinValue)
            {
                return "";
            }
            var prefix = "Exp Last updated: ";
            if (elapsed.TotalHours >= 24)
            {
                return prefix + (int)elapsed.TotalDays + " days ago";
            }

            if (elapsed.TotalHours >= 1)
            {
                return prefix + (int)elapsed.TotalHours + " hours ago";
            }

            if (elapsed.TotalMinutes >= 1)
            {
                return prefix + (int)elapsed.TotalMinutes + " minutes ago";
            }

            return prefix + (int)elapsed.TotalSeconds + " seconds ago";
        }

        private Message[] Transform(AIConversationMessage[] msgs)
        {
            Message[] output = new Message[msgs.Length];
            for (var i = 0; i < msgs.Length; ++i)
            {
                msgs[i].DateSent = DateTime.UtcNow;
                output[i] = msgs[i].Message;
            }
            return output;
        }

        private async Task<AIConversation> HandleMessageResponseAsync(SessionInfo session, AIConversation conversation, AIConversationMessage prompt, AIConversationMessage response, bool useGPT4)
        {
            // not implemented yet.
            return conversation;
        }

        private async Task<AIConversation> HandleFunctionCallAsync(SessionInfo session, AIConversation conversation, AIConversationMessage prompt, AIConversationMessage response, bool useGPT4)
        {
            var functionCall = response.Message.FunctionCall;
            var function = functions.FirstOrDefault(x => x.Name == functionCall.Name);
            if (function != null)
            {
                // time to call!
                // we will always assume arguments will be an object.
                object? result = null;
                if (function.Parameters.Length > 0)
                {
                    // lets try resolving the arguments needed.
                    //functionCall.Arguments
                    result = function.Invoke(functionCall.Arguments);
                }
                else
                {
                    // this one is empty, ignore arguments
                    result = function.Invoke();
                }

                conversation.Add(Message.CreateFunctionResult(function.Name, result));

                if (function.PreventDefault)
                {
                    return conversation;
                }

                // we have to post the conversation again to ensure that the ai can give a proper response.
                return await SendConversationAsync(conversation, useGPT4);
            }
            return conversation;
        }

        private bool IsValidSession(SessionInfo session)
        {
            if (session == null) return false;
            if (!session.Authenticated || !session.Administrator || session.UserId == Guid.Empty)
                return false;

            return true;
        }

    }
}
