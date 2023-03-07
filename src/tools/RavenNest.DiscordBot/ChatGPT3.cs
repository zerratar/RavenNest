using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace RavenNest.DiscordBot
{
    public class ImageRequest
    {
        [JsonPropertyName("prompt")]
        public string? Prompt
        {
            get;
            set;
        }

        [JsonPropertyName("n")]
        public int? Count
        {
            get;
            set;
        }

        [JsonPropertyName("size")]
        public string Size
        {
            get;
            set;
        }

        public static ImageRequest Create(string prompt, string size = "512x512", int count = 1)
        {
            return new ImageRequest
            {
                Count = count,
                Prompt = prompt,
                Size = size
            };
        }
    }

    public class ChatCompletionRequest
    {
        [JsonPropertyName("model")]
        public string? Model
        {
            get;
            set;
        }
        [JsonPropertyName("messages")]
        public ChatMessage[] Messages
        {
            get;
            set;
        }
    }

    public class ChatMessage
    {

        [JsonPropertyName("role")]
        public string? Role { get; set; }
        [JsonPropertyName("content")]
        public string? Content { get; set; }

        internal static ChatMessage Create(string role, string prompt)
        {
            return new ChatMessage
            {
                Role = role,
                Content = prompt,
            };
        }
    }

    public class ImageResponse
    {
        [JsonPropertyName("data")]
        public ImageResponseItem[] Data
        {
            get;
            set;
        }

        [JsonPropertyName("created")]
        public long? Created { get; set; }
    }

    public class ImageResponseItem
    {
        [JsonPropertyName("url")]
        public string Url { get; set; }
    }

    public class ChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public List<ChatGPTChoice>? Choices
        {
            get;
            set;
        }
        [JsonPropertyName("usage")]
        public ChatGPTUsage? Usage
        {
            get;
            set;
        }
    }
    public class ChatGPTUsage
    {
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens
        {
            get;
            set;
        }
        [JsonPropertyName("completion_token")]
        public int CompletionTokens
        {
            get;
            set;
        }
        [JsonPropertyName("total_tokens")]
        public int TotalTokens
        {
            get;
            set;
        }
    }

    public class ChatGPTChoice
    {
        [JsonPropertyName("index")]
        public int? Index
        {
            get;
            set;
        }

        [JsonPropertyName("finish_reason")]
        public string? FinishReason
        {
            get;
            set;
        }

        [JsonPropertyName("message")]
        public ChatMessage Message
        {
            get;
            set;
        }
    }

    public interface IOpenAI
    {
        Task<ChatCompletionResponse> GetCompletionAsync(string prompt, params ChatMessage[] previousMessages);

        Task<ImageResponse> GenerateImageAsync(string prompt, string size = "512x512", int count = 1);
    }

    public interface IOpenAISettings
    {
        string AccessToken { get; }
    }

    public class OpenAITokenString : IOpenAISettings
    {
        public string AccessToken { get; }

        public OpenAITokenString(string accessToken)
        {
            AccessToken = accessToken;
        }
    }

    public class OpenAI : IOpenAI, IDisposable
    {
        private bool disposed;
        private readonly HttpClient client;
        private readonly IOpenAISettings settings;

        public OpenAI(IOpenAISettings settings)
        {
            this.settings = settings;
            client = new HttpClient();
        }

        public async Task<ImageResponse> GenerateImageAsync(string prompt, string size = "512x512", int count = 1)
        {
            var req = ImageRequest.Create(prompt, size, count);
            ImageResponse response = null;
            using (var httpReq = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/images/generations"))
            {
                httpReq.Headers.Add("Authorization", $"Bearer {settings.AccessToken}");
                var requestString = JsonSerializer.Serialize(req);
                httpReq.Content = new StringContent(requestString, Encoding.UTF8, "application/json");
                using (var httpResponse = await client.SendAsync(httpReq))
                {
                    var responseString = await httpResponse.Content.ReadAsStringAsync();
                    if (!string.IsNullOrWhiteSpace(responseString))
                    {
                        response = JsonSerializer.Deserialize<ImageResponse>(responseString);
                    }

                    return response;
                }
            }
        }

        public async Task<ChatCompletionResponse> GetCompletionAsync(string prompt, params ChatMessage[] previousMessages)
        {
            var msgs = new List<ChatMessage>();
            msgs.AddRange(previousMessages);
            msgs.Add(ChatMessage.Create("user", prompt));
            var completionRequest = new ChatCompletionRequest
            {
                //Model = "text-davinci-003",
                Model = "gpt-3.5-turbo",
                //Model = "davinci:ft-shinobytes-2023-02-20-14-02-16",
                // Prompt = "What if Nicholas Cage played the lead role in Superman?",
                Messages = msgs.ToArray()
            };

            ChatCompletionResponse completionResponse = null;
            using (var httpReq = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions"))
            {
                httpReq.Headers.Add("Authorization", $"Bearer {settings.AccessToken}");
                var requestString = JsonSerializer.Serialize(completionRequest);
                httpReq.Content = new StringContent(requestString, Encoding.UTF8, "application/json");
                using (var httpResponse = await client.SendAsync(httpReq))
                {
                    var responseString = await httpResponse.Content.ReadAsStringAsync();
                    if (!string.IsNullOrWhiteSpace(responseString))
                    {
                        completionResponse = JsonSerializer.Deserialize<ChatCompletionResponse>(responseString);
                    }

                    return completionResponse;
                }
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            client.Dispose();
        }
    }
}
