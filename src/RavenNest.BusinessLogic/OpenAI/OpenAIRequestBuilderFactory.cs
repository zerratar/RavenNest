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

using System;

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
            private IOpenAIModelProvider modelProvider;

            public OpenAIRequestBuilder(IOpenAIModelProvider modelProvider)
            {
                this.modelProvider = modelProvider;
            }


            /*


             var useGPT4 = selection == OpenAIModelSelection.GPT4;
            var canUseGPT4 = selection == OpenAIModelSelection.Any || useGPT4;

            // selection is not perfect, but at least we can make sure that if we start to reach a huge amount of tokens we should use the bigger one.
            var model = request.TotalTokenCount > modelProvider.GPT35_4K.MaxPromptTokens ? modelProvider.GPT35_16K : modelProvider.GPT35_4K;

            if (canUseGPT4)
            {
                model = modelProvider.Get(request.TotalTokenCount, selection);
            }
  

            var msgs = new List<Message>();
            var promptTokenCount = GPT3Tokenizer.GetTokenCount(prompt);
            var useGPT4 = selection == OpenAIModelSelection.GPT4;
            var canUseGPT4 = selection == OpenAIModelSelection.Any || useGPT4;

            if (knowledgeBase == null && messages.Count > 0 && messages[0].Role == "system")
            {
                var history = messages.ToList();
            knowledgeBase = messages[0];
                history.RemoveAt(0);
                messages = history;
            }

        var knowledgeBaseTokenCount = knowledgeBase != null ? GPT3Tokenizer.GetTokenCount(knowledgeBase.Content) : 0;

        // max tokens are 4000 but this is included with response.
        // so only include messages up to 2500 tokens to give some room for the response.
        var tokenCount = promptTokenCount + knowledgeBaseTokenCount;
        var maxTokens = canUseGPT4 ? modelProvider.GPT4_8K.MaxPromptTokens : modelProvider.GPT35_16K.MaxPromptTokens;
            for (var i = messages.Count - 1; i >= 0; --i)
            {
                var msg = messages[i];
        var msgToken = GPT3Tokenizer.GetTokenCount(msg.Content);
                if (msgToken + tokenCount <= maxTokens)
                {
                    msgs.Insert(0, msg);
                    tokenCount += msgToken;
                    continue;
                }

                break;
            }

if (knowledgeBase != null)
{
    msgs.Insert(0, knowledgeBase);
}

msgs.Add(Message.Create("user", prompt));
return new OpenAIMessagePrompt
{
    Messages = msgs,
    Prompt = prompt,
    TotalTokenCount = tokenCount
};


*/

            public IOpenAIRequestBuilder AddFunction(Function function)
            {
                throw new NotImplementedException();
            }

            public IOpenAIRequestBuilder AddMessages(Message[] messages)
            {
                throw new NotImplementedException();
            }

            public ChatCompletionRequest Build()
            {
                throw new NotImplementedException();
            }

            public IOpenAIRequestBuilder SetKnowledgeBase(string prompt)
            {
                throw new NotImplementedException();
            }

            public IOpenAIRequestBuilder SetModel(OpenAIModel model)
            {
                throw new NotImplementedException();
            }

            public IOpenAIRequestBuilder SetMostSuitableModel(OpenAIModelSelection modelSelection)
            {
                throw new NotImplementedException();
            }

            public IOpenAIRequestBuilder SetPrompt(string prompt)
            {
                throw new NotImplementedException();
            }
        }
    }
}
