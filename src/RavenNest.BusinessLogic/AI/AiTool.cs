using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RavenNest.BusinessLogic.AI
{
    /// <summary>
    ///     Runs a tool the model asked for, and returns whatever the model should be told about it.
    /// </summary>
    public delegate Task<string> AiToolHandler(JsonElement arguments, CancellationToken cancellationToken);

    /// <summary>
    ///     Something the model is allowed to do besides write text.
    /// </summary>
    /// <remarks>
    ///     <see cref="RequiresConfirmation"/> is the important field. A tool that only reads runs on
    ///     its own; a tool that moves items, spends coins, or cannot be undone stops the run and
    ///     hands a description of what it is about to do back to whoever is asking, and does not
    ///     happen unless they say yes.
    ///
    ///     <para>
    ///     That decision belongs to the tool, not to the caller and not to the model. A model that
    ///     can be talked into calling a tool can be talked into claiming it does not need
    ///     confirming, so it never gets asked.
    ///     </para>
    /// </remarks>
    public sealed class AiTool
    {
        public AiTool(
            string name,
            string description,
            string parametersJsonSchema,
            AiToolHandler handler,
            bool requiresConfirmation = false,
            Func<JsonElement, string> summarise = null)
        {
            Name = name;
            Description = description;
            ParametersJsonSchema = parametersJsonSchema;
            Handler = handler;
            RequiresConfirmation = requiresConfirmation;
            Summarise = summarise;
        }

        public string Name { get; }

        /// <summary>What it does, written for the model to decide when to reach for it.</summary>
        public string Description { get; }

        /// <summary>A JSON Schema object describing the arguments.</summary>
        public string ParametersJsonSchema { get; }

        public AiToolHandler Handler { get; }

        /// <summary>
        ///     Whether a person has to agree before this runs. True for anything that touches a
        ///     player's items or coins, or that cannot be taken back.
        /// </summary>
        public bool RequiresConfirmation { get; }

        /// <summary>
        ///     Turns the arguments into the sentence a person is asked to agree to. Without one the
        ///     confirmation falls back to the raw arguments, which is honest but hard to read.
        /// </summary>
        public Func<JsonElement, string> Summarise { get; }

        public string Describe(JsonElement arguments)
        {
            if (Summarise == null) return Description;

            try
            {
                var summary = Summarise(arguments);
                return string.IsNullOrWhiteSpace(summary) ? Description : summary;
            }
            catch
            {
                // A summariser that throws on odd arguments must not take the confirmation with it.
                // Falling back to the plain description still asks the question.
                return Description;
            }
        }

        /// <summary>
        ///     A tool that takes no arguments still needs a schema, and this is it.
        /// </summary>
        public const string NoParameters = "{\"type\":\"object\",\"properties\":{},\"required\":[],\"additionalProperties\":false}";
    }

    /// <summary>
    ///     A tool call that is waiting on somebody to agree to it.
    /// </summary>
    public sealed class AiPendingAction
    {
        public string CallId { get; set; }

        public string ToolName { get; set; }

        /// <summary>The arguments as the model sent them, kept so the call can be run on approval.</summary>
        public string ArgumentsJson { get; set; }

        /// <summary>What the person is being asked to agree to, in a sentence.</summary>
        public string Summary { get; set; }

        /// <summary>Where to pick the conversation back up.</summary>
        public string ResponseId { get; set; }
    }

    public sealed class AiRequest
    {
        /// <summary>The standing instructions: who the assistant is and what it may say.</summary>
        public string Instructions { get; set; }

        /// <summary>What is being asked, this time.</summary>
        public string Input { get; set; }

        public IReadOnlyList<AiTool> Tools { get; set; }

        /// <summary>Overrides the model configured in the admin panel. Rarely wanted.</summary>
        public string Model { get; set; }

        public int? MaxOutputTokens { get; set; }

        /// <summary>Continues an earlier exchange rather than starting a new one.</summary>
        public string PreviousResponseId { get; set; }
    }

    public sealed class AiResult
    {
        public bool Ok => Error == null && Pending == null;

        public string Text { get; set; }

        /// <summary>Set when the run stopped to ask permission. Nothing has happened yet.</summary>
        public AiPendingAction Pending { get; set; }

        /// <summary>Set when it did not work, written for a person rather than a log.</summary>
        public string Error { get; set; }

        /// <summary>The id to pass back as <see cref="AiRequest.PreviousResponseId"/>.</summary>
        public string ResponseId { get; set; }

        public static AiResult Failed(string error) => new AiResult { Error = error };
    }
}
