using Shinobytes.OpenAI.Models;
using System;

namespace RavenNest.Blazor.Services
{
    public class AIConversationMessage
    {
        public Message Message { get; set; }
        public DateTime Time { get; set; }
    }
}
