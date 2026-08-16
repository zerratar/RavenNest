using System.Collections.Generic;
using System.Linq;

namespace RavenNest.BusinessLogic.AI
{
    public sealed class AiModel
    {
        public AiModel(string id, string label, string note)
        {
            Id = id;
            Label = label;
            Note = note;
        }

        public string Id { get; }
        public string Label { get; }

        /// <summary>What it is good for, so picking one is not a guess.</summary>
        public string Note { get; }
    }

    /// <summary>
    ///     The models the admin panel offers.
    /// </summary>
    /// <remarks>
    ///     A list, not a free text box, because a typo in a model name fails at the point of use and
    ///     reads as "the AI is broken". It is not a closed list either: OpenAI ships models faster
    ///     than this gets deployed, so the field accepts anything typed into it and these are the
    ///     suggestions.
    ///
    ///     <para>
    ///     Everything here talks to the Responses API and supports tool calls, which the assistant
    ///     relies on. Anything from the gpt-4 era is deliberately absent.
    ///     </para>
    /// </remarks>
    public static class AiModels
    {
        public const string Default = "gpt-5.6-terra";

        public static readonly IReadOnlyList<AiModel> Suggested = new List<AiModel>
        {
            new AiModel("gpt-5.6-sol", "GPT-5.6 Sol", "The strongest of the three. Slowest and dearest; worth it for anything that has to reason."),
            new AiModel("gpt-5.6-terra", "GPT-5.6 Terra", "The balanced one, and the default. Fine for writing a post or answering a question."),
            new AiModel("gpt-5.6-luna", "GPT-5.6 Luna", "The cheap one. For high volume work where a wrong answer costs nothing."),
        };

        public static string LabelOf(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            return Suggested.FirstOrDefault(x => x.Id == id)?.Label ?? id;
        }
    }
}
