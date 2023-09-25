using Shinobytes.OpenAI.Models;
using System;

namespace RavenNest.BusinessLogic.OpenAI.Conversations
{
    public class AIConversationMessage
    {
        public Guid Id { get; set; }
        public Message Message { get; set; }
        public DateTime Created { get; set; }
        public DateTime DateSent { get; set; }
        public bool IsSent => DateSent > DateTime.UnixEpoch;
    }
}
