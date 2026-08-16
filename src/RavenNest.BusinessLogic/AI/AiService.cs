using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Settings;

// The package's root namespace is OpenAI, and this assembly already has a
// RavenNest.BusinessLogic.OpenAI namespace of its own from the old hand written client, which wins
// the name lookup from in here. global:: says which one is meant.
using global::OpenAI.Responses;

namespace RavenNest.BusinessLogic.AI
{
    public interface IAiService
    {
        bool IsConfigured { get; }

        string ModelInUse { get; }

        Task<AiResult> AskAsync(AiRequest request, CancellationToken cancellationToken = default);

        Task<AiResult> ContinueAsync(
            AiPendingAction action,
            bool approved,
            AiRequest request,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    ///     The one way this server talks to OpenAI.
    /// </summary>
    /// <remarks>
    ///     Built on the official SDK and the Responses API rather than the hand written client in
    ///     Shinobytes.OpenAI. That client predates tool calling as it now works, pins a model from
    ///     two families ago, and has to be taught every new field by hand. The old one stays where
    ///     it is until the things using it are moved; nothing new should reach for it.
    ///
    ///     <para>
    ///     The key and the model come from the admin panel, read fresh on every call rather than
    ///     captured at construction. This is a singleton and the server does not get restarted to
    ///     change a setting, so a captured key would mean the panel appears to work and does
    ///     nothing until the next deploy.
    ///     </para>
    ///
    ///     <para>
    ///     Tool calls that a tool has marked as needing confirmation stop the run and come back as
    ///     <see cref="AiResult.Pending"/>. Nothing has happened at that point: the model has said
    ///     what it wants to do and is waiting. Approving it goes through
    ///     <see cref="ContinueAsync"/>, which is the only place a confirmed tool is ever run. That
    ///     matters more here than in most places, because the tools this will grow can move a
    ///     player's items and spend their coins, and an assistant that does that on its own say so
    ///     is a bug with a support ticket attached.
    ///     </para>
    /// </remarks>
    public class AiService : IAiService
    {
        /// <summary>
        ///     How many times the model may call tools and be asked again before the run is stopped.
        ///     A model that has misunderstood a tool will keep calling it, and each round is a
        ///     billed request, so the loop is bounded rather than trusted.
        /// </summary>
        private const int MaxToolRounds = 8;

        private readonly IServerSettingsProvider settings;
        private readonly ILogger<AiService> logger;

        public AiService(IServerSettingsProvider settings, ILogger<AiService> logger)
        {
            this.settings = settings;
            this.logger = logger;
        }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);

        public string ModelInUse
        {
            get
            {
                var configured = settings.GetString(ServerSettingsRegistry.OpenAiModel);
                return string.IsNullOrWhiteSpace(configured) ? AiModels.Default : configured;
            }
        }

        private string ApiKey => settings.GetString(ServerSettingsRegistry.OpenAiAccessToken);

        public async Task<AiResult> AskAsync(AiRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null) return AiResult.Failed("Nothing was asked.");

            var client = CreateClient(out var unavailable);
            if (client == null) return unavailable;

            var options = BuildOptions(request);
            if (!string.IsNullOrWhiteSpace(request.Input))
            {
                options.InputItems.Add(ResponseItem.CreateUserMessageItem(request.Input));
            }

            return await RunAsync(client, options, request, cancellationToken);
        }

        public async Task<AiResult> ContinueAsync(
            AiPendingAction action,
            bool approved,
            AiRequest request,
            CancellationToken cancellationToken = default)
        {
            if (action == null) return AiResult.Failed("There is nothing waiting to be confirmed.");

            var client = CreateClient(out var unavailable);
            if (client == null) return unavailable;

            var tool = FindTool(request, action.ToolName);
            if (tool == null)
            {
                // The tool list changed under a pending confirmation, most likely a deploy. Running
                // something else, or guessing, is worse than saying so.
                return AiResult.Failed("That action is no longer available. Ask again.");
            }

            string output;
            if (!approved)
            {
                output = "The person declined this action. It was not carried out. Do not try it again; " +
                         "say what you would have done and stop.";
                logger.LogInformation("AI action '" + action.ToolName + "' was declined.");
            }
            else
            {
                logger.LogWarning("AI action '" + action.ToolName + "' was approved and is being carried out.");
                output = await InvokeAsync(tool, action.ArgumentsJson, cancellationToken);
            }

            var options = BuildOptions(request);
            options.PreviousResponseId = action.ResponseId;
            options.InputItems.Add(ResponseItem.CreateFunctionCallOutputItem(action.CallId, output));

            return await RunAsync(client, options, request, cancellationToken);
        }

        /// <summary>
        ///     Asks, runs whatever tools come back, and asks again, until there is an answer or
        ///     something needs confirming.
        /// </summary>
        private async Task<AiResult> RunAsync(
            ResponsesClient client,
            CreateResponseOptions options,
            AiRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                for (var round = 0; round < MaxToolRounds; round++)
                {
                    var response = await client.CreateResponseAsync(options, cancellationToken);
                    var result = response?.Value;

                    if (result == null)
                    {
                        return AiResult.Failed("OpenAI did not answer. Try again in a moment.");
                    }

                    if (result.Error != null)
                    {
                        // The message from OpenAI is usually the useful one, and it is about the
                        // request rather than about the key.
                        logger.LogError("OpenAI returned an error: " + result.Error.Message);
                        return AiResult.Failed(result.Error.Message);
                    }

                    var calls = result.OutputItems.OfType<FunctionCallResponseItem>().ToList();
                    if (calls.Count == 0)
                    {
                        return new AiResult
                        {
                            Text = result.GetOutputText(),
                            ResponseId = result.Id
                        };
                    }

                    // A confirmable call ends the round even if other calls came with it. Running
                    // the harmless half of a batch and then asking about the rest leaves the person
                    // agreeing to something that has already partly happened.
                    foreach (var call in calls)
                    {
                        var tool = FindTool(request, call.FunctionName);
                        if (tool == null || !tool.RequiresConfirmation) continue;

                        return new AiResult
                        {
                            ResponseId = result.Id,
                            Pending = new AiPendingAction
                            {
                                CallId = call.CallId,
                                ToolName = call.FunctionName,
                                ArgumentsJson = call.FunctionArguments?.ToString(),
                                Summary = Describe(tool, call.FunctionArguments?.ToString()),
                                ResponseId = result.Id
                            }
                        };
                    }

                    var next = BuildOptions(request);
                    next.PreviousResponseId = result.Id;

                    foreach (var call in calls)
                    {
                        var tool = FindTool(request, call.FunctionName);
                        var output = tool == null
                            ? "There is no tool by that name."
                            : await InvokeAsync(tool, call.FunctionArguments?.ToString(), cancellationToken);

                        next.InputItems.Add(ResponseItem.CreateFunctionCallOutputItem(call.CallId, output));
                    }

                    options = next;
                }

                logger.LogWarning("An AI request went round the tool loop " + MaxToolRounds + " times and was stopped.");
                return AiResult.Failed("That took too many steps and was stopped. Try asking for less at once.");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exc)
            {
                // Deliberately not the exception text to the caller. It can carry request bodies,
                // and request bodies here can carry whatever a player typed.
                logger.LogError("An AI request failed: " + exc);
                return AiResult.Failed("Could not reach OpenAI. Check the API key in server settings, then try again.");
            }
        }

        private async Task<string> InvokeAsync(AiTool tool, string argumentsJson, CancellationToken cancellationToken)
        {
            try
            {
                var arguments = Parse(argumentsJson);
                var output = await tool.Handler(arguments, cancellationToken);
                return string.IsNullOrWhiteSpace(output) ? "Done." : output;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exc)
            {
                // Told to the model rather than thrown, so it can say what went wrong instead of the
                // whole exchange dying. The detail goes to the log, not into the conversation.
                logger.LogError("The AI tool '" + tool.Name + "' failed: " + exc);
                return "That did not work. Tell the person it failed and do not try it again.";
            }
        }

        private string Describe(AiTool tool, string argumentsJson)
        {
            try
            {
                return tool.Describe(Parse(argumentsJson));
            }
            catch
            {
                return tool.Description;
            }
        }

        private static JsonElement Parse(string argumentsJson)
        {
            if (string.IsNullOrWhiteSpace(argumentsJson))
            {
                // A tool with no parameters is called with nothing at all, and handlers should not
                // each have to cope with null.
                using var empty = JsonDocument.Parse("{}");
                return empty.RootElement.Clone();
            }

            using var document = JsonDocument.Parse(argumentsJson);
            return document.RootElement.Clone();
        }

        private static AiTool FindTool(AiRequest request, string name)
        {
            if (request?.Tools == null || string.IsNullOrWhiteSpace(name)) return null;
            return request.Tools.FirstOrDefault(x => x.Name == name);
        }

        private CreateResponseOptions BuildOptions(AiRequest request)
        {
            var options = new CreateResponseOptions
            {
                Model = string.IsNullOrWhiteSpace(request.Model) ? ModelInUse : request.Model,
                Instructions = request.Instructions,
                MaxOutputTokenCount = request.MaxOutputTokens
            };

            if (!string.IsNullOrWhiteSpace(request.PreviousResponseId))
            {
                options.PreviousResponseId = request.PreviousResponseId;
            }

            if (request.Tools != null)
            {
                foreach (var tool in request.Tools)
                {
                    options.Tools.Add(ResponseTool.CreateFunctionTool(
                        tool.Name,
                        BinaryData.FromString(tool.ParametersJsonSchema ?? AiTool.NoParameters),
                        strictModeEnabled: true,
                        functionDescription: tool.Description));
                }
            }

            return options;
        }

        /// <summary>
        ///     A client per call. It is a thin wrapper over a pooled pipeline, and building it here
        ///     is what lets the key be changed in the admin panel and take effect on the next
        ///     request rather than on the next restart.
        /// </summary>
        private ResponsesClient CreateClient(out AiResult unavailable)
        {
            var key = ApiKey;
            if (string.IsNullOrWhiteSpace(key))
            {
                unavailable = AiResult.Failed("No OpenAI API key is set. Add one under Server settings.");
                return null;
            }

            unavailable = null;
            return new ResponsesClient(key);
        }
    }
}
