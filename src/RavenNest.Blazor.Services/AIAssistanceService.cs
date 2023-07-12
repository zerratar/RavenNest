using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using RavenNest.BusinessLogic.Data;
using RavenNest.Models;
using Shinobytes.OpenAI;
using Shinobytes.OpenAI.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Services
{
    public class AIAssistanceService : RavenNestService
    {
        private readonly GameData gameData;
        private readonly IOpenAIClient openAI;
        private readonly AIConversationManager conversations;

        public AIAssistanceService(
            GameData gameData,
            IOpenAIClient openAIClient,
            IHttpContextAccessor accessor,
            SessionInfoProvider sessionInfoProvider)
            : base(accessor, sessionInfoProvider)
        {
            this.gameData = gameData;
            this.openAI = openAIClient;
            this.conversations = new AIConversationManager(gameData);
        }

        public MarkupString FormatMessage(string input)
        {
            return new MarkupString(Markdig.Markdown.ToHtml(input));
        }

        public bool RemoveAllConversations()
        {
            var session = GetSession();
            var uid = session.UserId;
            return conversations.RemoveAll(uid);
        }

        public AIConversation ClearConversationHistory(Guid conversationId)
        {
            var session = GetSession();
            var uid = session.UserId;
            var conversation = conversations.Get(conversationId);
            if (conversation == null) return null;
            if (conversation.UserId != uid) return null;
            return conversations.ClearConversation(conversation);
        }

        public AIConversation GetLatestConversion()
        {
            var session = GetSession();
            return conversations.GetLatestOrCreate(session.UserId);
        }

        public async Task<AIConversation> SendMessageAsync(string input, bool useGPT4)
        {
            var session = GetSession();
            var uid = session.UserId;

            var conversation = conversations.GetLatestOrCreate(uid);
            var prompt = conversation.GetMessageByContent(input);

            // if we don't have a prompt message, generate one here. this will be better as we will then have a reference
            // to the same prompt later.
            if (prompt == null)
            {
                prompt = conversation.Add(input, ChatMessageRole.User);
            }

            var messages = conversation.GetMessages();

            var builder = openAI.GetRequestBuilder();
            var request = builder
                .SetKnowledgeBase(KnowledgeBase)
                .AddFunctions(GetFunctions())
                .AddMessages(messages)
                .Build(useGPT4 ? OpenAIModelSelection.GPT4 : OpenAIModelSelection.GPT3_5);

            var result = await openAI.GetCompletionAsync(request, System.Threading.CancellationToken.None);
            var choice = result.Choices.FirstOrDefault();

            var response = conversation.Add(choice.Message);

            switch (choice.FinishReason)
            {
                case "function_call":
                    return await HandleFunctionCallAsync(session, conversation, prompt, response);
                default:
                    return await HandleMessageResponseAsync(session, conversation, prompt, response);
            }
        }

        public async Task<AIConversation> SendConversationAsync(AIConversation conversation, bool useGPT4)
        {
        }

        private async Task<AIConversation> HandleMessageResponseAsync(SessionInfo session, AIConversation conversation, AIConversationMessage prompt, AIConversationMessage response)
        {
            return conversation;
        }

        private async Task<AIConversation> HandleFunctionCallAsync(SessionInfo session, AIConversation conversation, AIConversationMessage prompt, AIConversationMessage response)
        {
            var functionCall = response.Message.FunctionCall;
            return conversation;
        }


        private Function[] GetFunctions()
        {
            return new Function[0];
        }

        private static readonly string KnowledgeBase = "You are an AI Assistant for Administrators of the Twitch Idle RPG game Ravenfall. You will take upon any request and try to help out any way you can. You are able to do administrative actions that directly interacts with the backend, gameserver, APIs and website. Use provided functions when necessary.";
    }
}
