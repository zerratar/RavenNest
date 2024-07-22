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

using System.Collections.Generic;
using Shinobytes.OpenAI.Models;
using Message = Shinobytes.OpenAI.Models.Message;

namespace Shinobytes.OpenAI
{
    public class OpenAIRequestBuilderFactory : IOpenAIRequestBuilderFactory
    {
        private readonly IOpenAIModelProvider modelProvider;

        public OpenAIRequestBuilderFactory(IOpenAIModelProvider modelProvider)
        {
            this.modelProvider = modelProvider;
        }

        public IOpenAIRequestBuilder Create()
        {
            return new OpenAIRequestBuilder(modelProvider);
        }

        private class OpenAIRequestBuilder : IOpenAIRequestBuilder
        {
            private readonly IOpenAIModelProvider modelProvider;
            private readonly List<Message> messages = new List<Message>();
            private readonly List<Function> functions = new List<Function>();
            private Message knowledgebase;
            private Message prompt;

            public OpenAIRequestBuilder(IOpenAIModelProvider modelProvider)
            {
                this.modelProvider = modelProvider;
            }

            public IOpenAIRequestBuilder AddFunction(Function function)
            {
                this.functions.Add(function);
                return this;
            }

            public IOpenAIRequestBuilder AddFunctions(Function[] functions)
            {
                this.functions.AddRange(functions);
                return this;
            }

            public IOpenAIRequestBuilder AddMessages(Message[] messages)
            {
                this.messages.AddRange(messages);
                return this;
            }

            public IOpenAIRequestBuilder SetKnowledgeBase(string prompt)
            {
                this.knowledgebase = Message.Create(MessageRole.System, prompt);
                return this;
            }

            public IOpenAIRequestBuilder SetPrompt(string prompt)
            {
                this.prompt = Message.Create(MessageRole.User, prompt);
                return this;
            }

            public IOpenAIRequestBuilder SetPrompt(Message prompt)
            {
                this.prompt = prompt;
                return this;
            }

            public ChatCompletionRequest Build(OpenAIModelSelection modelSelection)
            {
                var requestMessages = new List<Message>();
                var tokenCount = 0;

                var useGPT4 = modelSelection == OpenAIModelSelection.GPT4o;
                var canUseGPT4 = modelSelection == OpenAIModelSelection.Any || useGPT4;

                // get the minimum required token count
                if (knowledgebase != null) tokenCount += GPT3Tokenizer.GetTokenCount(knowledgebase.Content);
                if (prompt != null) tokenCount += GPT3Tokenizer.GetTokenCount(prompt.Content);

                // determine how many of the messages we can include in the request
                // based on the token usage

                // 1. get the max token count for the possible models we can use
                var maxTokenCount = useGPT4 ? modelProvider.GPT4o.MaxPromptTokens : modelProvider.GPT4oMini.MaxPromptTokens;

                // 2. go backwards, and add messages until we reach the max token count
                for (var i = messages.Count - 1; i >= 0; --i)
                {
                    var msg = messages[i];
                    var msgToken = GPT3Tokenizer.GetTokenCount(msg.Content);
                    if (msgToken + tokenCount <= maxTokenCount)
                    {
                        requestMessages.Insert(0, msg);
                        tokenCount += msgToken;
                        continue;
                    }

                    break;
                }

                // 3. insert knowledgebase and prompt
                if (knowledgebase != null) requestMessages.Insert(0, knowledgebase);
                if (prompt != null) requestMessages.Add(prompt);
                

                // 4. select an appropriate model
                var model = canUseGPT4
                        ? modelProvider.Get(tokenCount, modelSelection) // if "any" selected then theres a 10% chance for GPT4 if tokens allows for it.
                        : modelProvider.GPT4oMini;

                // 5. yay
                return new ChatCompletionRequest
                {
                    Functions = functions.Count > 0 ? functions.ToArray() : null,
                    Messages = requestMessages.ToArray(),
                    Model = model.Name
                };
            }
        }
    }
}
