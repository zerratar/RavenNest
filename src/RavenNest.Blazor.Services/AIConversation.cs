using Shinobytes.OpenAI.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RavenNest.Blazor.Services
{
    public class AIConversation
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Title { get; set; }
        public List<AIConversationMessage> Messages { get; set; }
        public DateTime StartTime { get; set; }

        private AIConversationManager manager;

        /// <summary>
        ///     Gets all messages, however if the last message is the same as provided prompt, the last message will be excluded.
        /// </summary>
        /// <param name="prompt"></param>
        /// <returns></returns>
        public Message[] GetMessages(string prompt)
        {
            if (Messages.Count == 0)
            {
                return new Message[0];
            }

            var lastMessage = Messages[^1];
            var lastMessageContent = lastMessage.Message.Content;
            if (!string.IsNullOrEmpty(lastMessageContent) && MessageMatch(lastMessageContent, prompt))
            {
                return Messages.Take(Messages.Count - 1).Select(x => x.Message).ToArray();
            }

            return Messages.Select(x => x.Message).ToArray();
        }

        /// <summary>
        ///     Gets all messages with the exception of the provided messages.
        /// </summary>
        /// <param name="exception"></param>
        /// <returns></returns>
        public Message[] GetMessages(params Message[] exception)
        {
            if (Messages.Count == 0)
            {
                return new Message[0];
            }
            var msgs = new List<Message>();
            foreach (var msg in Messages)
            {
                if (exception.Any(x => x.Equals(msg.Message)))
                {
                    continue;
                }

                msgs.Add(msg.Message);
            }

            return msgs.ToArray();
        }

        /// <summary>
        ///     Gets the last message that has the same prompt.
        /// </summary>
        /// <param name="prompt"></param>
        /// <returns></returns>
        internal AIConversationMessage GetMessageByContent(string prompt)
        {
            if (Messages.Count == 0) return null;
            for (var i = Messages.Count - 1; i >= 0; i--)
            {
                var message = Messages[i];
                var content = message.Message.Content;
                if (!string.IsNullOrEmpty(content) && MessageMatch(content, prompt))
                {
                    return message;
                }
            }
            return null;
        }

        private bool MessageMatch(string a, string b)
        {
            if (string.IsNullOrEmpty(b))
            {
                return false;
            }

            return a.Equals(b, StringComparison.OrdinalIgnoreCase);
        }

        public DateTime GetLastActivity()
        {
            var a = StartTime;
            if (Messages != null)
            {
                var b = Messages.Max(x => x.Time);
                if (b > a) return b;
            }
            return a;
        }

        internal void Init(AIConversationManager manager)
        {
            this.manager = manager;
        }

        public void Add(AIConversationMessage message)
        {
            if (Messages.Any(x => x.Message.Role == message.Message.Role && x.Message.Equals(message.Message)))
            {
                return;
            }

            if (manager == null)
            {
                throw new NotSupportedException("You must call Init(AIConversationManager) before you can call this method.");
            }

            Messages.Add(message);
            manager.SaveConversationsJson(UserId);
        }

        public AIConversationMessage Add(Message prompt)
        {
            var existing = Messages.FirstOrDefault(x => x.Message.Equals(prompt));
            if (existing != null)
            {
                return existing;
            }

            if (manager == null)
            {
                throw new NotSupportedException("You must call Init(AIConversationManager) before you can call this method.");
            }

            var msg = new AIConversationMessage
            {
                Message = prompt,
                Time = DateTime.UtcNow
            };

            Messages.Add(msg);
            manager.SaveConversationsJson(UserId);
            return msg;
        }

        public AIConversationMessage Add(string prompt, ChatMessageRole user)
        {

            throw new NotImplementedException();
        }
    }
}
