/* 
 * This file is part of RavenBot (https://www.github.com/zerratar/ravenbot/).
 * Copyright (c) 2017-2023 Shinobytes, Karl Patrik Johansson, zerratar@gmail.com
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.  
 **/

using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System;
using Newtonsoft.Json;

using Shinobytes.OpenAI.Models;
using System.Threading;
using System.IO;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic;
using Microsoft.Extensions.Options;
using Message = Shinobytes.OpenAI.Models.Message;
using Microsoft.Extensions.Logging;

namespace Shinobytes.OpenAI
{
    public class OpenAIClient : IOpenAIClient, IDisposable
    {
        private bool disposed;
        private readonly HttpClient client;
        private readonly OpenAISettings settings;
        private readonly ILogger<OpenAIClient> logger;
        private readonly IOpenAIRequestBuilderFactory reqBuilderFactory;

        public OpenAIClient(
            ILogger<OpenAIClient> logger,
            IOptions<OpenAISettings> settings,
            IOpenAIRequestBuilderFactory reqBuilderFactory)
        {
            this.settings = settings.Value;
            this.logger = logger;
            this.reqBuilderFactory = reqBuilderFactory;
            client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(3);
        }

        public async Task<ImageResponse> GenerateImageAsync(string prompt, CancellationToken cancellationToken, string size = "512x512", int count = 1)
        {
            return await RequestAsync<ImageRequest, ImageResponse>("https://api.openai.com/v1/images/generations", ImageRequest.Create(prompt, size, count), cancellationToken);
        }

        public Task<ChatCompletionResponse> GetCompletionAsync(string prompt, CancellationToken cancellationToken, params Message[] previousMessages)
        {
            //BuildPrompt(prompt, previousMessages)
            var builder = GetRequestBuilder();
            var request = builder.AddMessages(previousMessages).SetPrompt(prompt).Build(OpenAIModelSelection.GPT4oMini);
            return GetCompletionAsync(request, cancellationToken);
        }

        public IOpenAIRequestBuilder GetRequestBuilder()
        {
            return reqBuilderFactory.Create();
        }

        public async Task<ChatCompletionResponse> GetCompletionAsync(ChatCompletionRequest request, CancellationToken cancellationToken)
        {
            return await RequestAsync<ChatCompletionRequest, ChatCompletionResponse>("https://api.openai.com/v1/chat/completions", request, cancellationToken);
        }

        private static readonly FunctionConverter functionConverter = new FunctionConverter();

        private async Task<TResult> RequestAsync<TRequest, TResult>(string url, TRequest model, CancellationToken cancellationToken)
        {
            var s = settings;
            using (var httpReq = new HttpRequestMessage(HttpMethod.Post, url))
            {
                httpReq.Headers.Add("Authorization", $"Bearer {s.AccessToken}");
                var requestString = JsonConvert.SerializeObject(model, Formatting.None, functionConverter);
                httpReq.Content = new StringContent(requestString, Encoding.UTF8, "application/json");
                using (var httpResponse = await client.SendAsync(httpReq))
                {
                    var responseString = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
                    if (!string.IsNullOrWhiteSpace(responseString))
                    {
                        var obj = JsonConvert.DeserializeObject<TResult>(responseString);
                        if (obj == null)
                        {
                            LogErrorResponse(responseString);
                        }
                        return obj;
                    }

                    return default;
                }
            }
        }

        private void LogErrorResponse(string responseString)
        {
            var folder = Path.Combine(FolderPaths.GeneratedDataPath, FolderPaths.OpenAILogs);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var path = Path.Combine(folder, DateTime.Now.ToString("yyyy-MM-dd") + ".txt");
            File.AppendAllText(path, "[" + DateTime.Now + "]\t" + responseString);
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
