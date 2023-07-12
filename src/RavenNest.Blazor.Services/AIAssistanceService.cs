using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;
using RavenNest.Models;
using Shinobytes.OpenAI;
using Shinobytes.OpenAI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Services
{
    public interface IAIAssistanceFunctionCallbacks
    {
        DataModels.User GetUserById(Guid userId);
        int GetActiveSessionsCount();
    }

    public class AIAssistanceFunctionCallbacks : IAIAssistanceFunctionCallbacks
    {
        private readonly GameData gameData;

        public AIAssistanceFunctionCallbacks(GameData gameData)
        {
            this.gameData = gameData;
        }

        public int GetActiveSessionsCount()
        {
            return gameData.GetActiveSessions().Count;
        }

        public User GetUserById(Guid userId)
        {
            return gameData.GetUser(userId);
        }
    }

    public class AIAssistanceService : RavenNestService
    {
        private readonly GameData gameData;
        private readonly IOpenAIClient openAI;
        private readonly IAIAssistanceFunctionCallbacks functionCallbacks;
        private readonly AIConversationManager conversations;
        private readonly Function[] functions;

        public AIAssistanceService(
            GameData gameData,
            IOpenAIClient openAIClient,
            IHttpContextAccessor accessor,
            IAIAssistanceFunctionCallbacks functionCallbacks,
            SessionInfoProvider sessionInfoProvider)
            : base(accessor, sessionInfoProvider)
        {
            this.gameData = gameData;
            this.openAI = openAIClient;
            this.functionCallbacks = functionCallbacks;
            this.conversations = new AIConversationManager(gameData);
            this.functions = new[]
            {
                Function.Create(ClearCurrentConversation, this, description:"Clears the current conversation's chat history with the AI Assistant.", preventDefault: true),
                Function.Create(GetSession, this, description:"Gets the session info of the current logged in user that contains details such as username, whether or not user is an administator and more."),
                Function.Create<Guid, User>(functionCallbacks.GetUserById, functionCallbacks, description:"Gets details about the user using a provided guid user id."),
                Function.Create(functionCallbacks.GetActiveSessionsCount, functionCallbacks, description:"Gets active amount of game sessions, how many twitch streamers that are currently running ravenfall")
            };
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

        public bool RemoveAllConversations()
        {
            var session = GetSession();
            if (!IsValidSession(session)) return false;

            var uid = session.UserId;
            return conversations.RemoveAll(uid);
        }

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
                .SetKnowledgeBase(KnowledgeBase)
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

        private static readonly string KnowledgeBase = "You are an AI Assistant for Administrators of the Twitch Idle RPG game Ravenfall. You will take upon any request and try to help out any way you can. You are able to do administrative actions that directly interacts with the backend, gameserver, APIs and website. Use provided functions when necessary. Ravenfall was created by a Swedish Developer named Karl but goes under the username/nick Zerratar, when referring to the creator always use Zerratar unless someone asks for the real name.";
    }

    //public class AIFunctionRegistry
    //{
    //    public FunctionReference<TOutput> Register<TInput, TOutput>(Action<TInput> action)
    //    {
    //    }
    //}
    //public class FunctionReference
}
