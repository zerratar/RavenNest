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

using Shinobytes.OpenAI.Models;

namespace Shinobytes.OpenAI
{
    public interface IOpenAIRequestBuilder
    {

        /// <summary>
        ///     Add historical messages to allow the model to continue on a conversation.
        /// </summary>
        /// <param name="messages"></param>
        /// <returns></returns>
        IOpenAIRequestBuilder AddMessages(Message[] messages);

        /// <summary>
        ///     Set a knowledge base to ensure that the message is included and that it will always be the very first message and assigned to "system"
        /// </summary>
        /// <param name="prompt"></param>
        /// <returns></returns>
        IOpenAIRequestBuilder SetKnowledgeBase(string prompt);
        /// <summary>
        ///     If a prompt is set, this will always be the last message in the request.
        /// </summary>
        /// <param name="prompt"></param>
        /// <returns></returns>
        IOpenAIRequestBuilder SetPrompt(string prompt);

        /// <summary>
        ///     If a prompt is set, this will always be the last message in the request.
        /// </summary>
        /// <param name="prompt"></param>
        /// <returns></returns>
        IOpenAIRequestBuilder SetPrompt(Message prompt);

        /// <summary>
        ///     Add a function that the model can call.
        /// </summary>
        /// <param name="function"></param>
        /// <returns></returns>
        IOpenAIRequestBuilder AddFunction(Function function);
        /// <summary>
        ///     Adds multiple functions that the model can call.
        /// </summary>
        /// <param name="functions"></param>
        /// <returns></returns>
        IOpenAIRequestBuilder AddFunctions(Function[] functions);
        /// <summary>
        ///     Builds the final completion request
        /// </summary>
        /// <returns></returns>
        ChatCompletionRequest Build(OpenAIModelSelection modelSelection);
    }
}
