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
using System;

using Shinobytes.OpenAI.Models;
using System.Linq;

namespace Shinobytes.OpenAI
{
    public class OpenAIModelProvider : IOpenAIModelProvider
    {
        private const double GPT4Chance = 0.10;

        private readonly Random random;
        private readonly List<OpenAIModel> models;

        public OpenAIModel GPT4oMini { get; }
        public OpenAIModel GPT4o { get; }

        public OpenAIModelProvider()
        {
            random = new Random();

            GPT4oMini = new OpenAIModel { Name = "gpt-4o-mini", MaxTokens = 128000, MaxPromptTokens = 64000, PriceInput = 0.003, PriceOutput = 0.0004 };
            GPT4o = new OpenAIModel { Name = "gpt-4o", MaxTokens = 128000, MaxPromptTokens = 64000, PriceInput = 0.03, PriceOutput = 0.06 };

            models = new List<OpenAIModel> {
                GPT4oMini,
                GPT4o
            };
        }

        public OpenAIModel Get(int tokenUsage, OpenAIModelSelection selection = OpenAIModelSelection.GPT4oMini)
        {
            var useGPT4 = selection == OpenAIModelSelection.GPT4o;
            var canUseGPT4 = selection == OpenAIModelSelection.Any || useGPT4;
            // we need to leave space for response
            // so if we use more than 60% of the max token count, we need to use a bigger model.

            if (useGPT4)
            {
                return GPT4o;
            }

            // we already know which models exists, so we will go by index. since we shouldnt accidently use gpt-4 randomly, or should we?
            if (canUseGPT4)
            {
                // its darn expensive with GPT4.
                if (tokenUsage < GPT4o.MaxPromptTokens && random.NextDouble() <= GPT4Chance)
                {
                    return GPT4o;
                }

                // if we are allowed to use GPT4, use it if we expect to use more than 4917 tokens
                var targetModel = models.OrderBy(x => random.Next()).FirstOrDefault(x => tokenUsage <= x.MaxPromptTokens);
                if (targetModel != null)
                {
                    return targetModel;
                }
            }

            return GPT4oMini;
        }
    }
}
