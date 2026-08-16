using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RavenNest.BusinessLogic.AI
{
    public sealed class AiTurn
    {
        public bool FromUser { get; set; }
        public string Text { get; set; }
    }

    /// <summary>
    ///     An exchange with the assistant that remembers where it got to.
    /// </summary>
    /// <remarks>
    ///     Only the visible turns are held here. The model's own working, its tool calls and their
    ///     results, stay on OpenAI's side and are picked up again by response id, which is what the
    ///     Responses API is for. That keeps this small and means a half finished exchange cannot be
    ///     reconstructed wrongly from a partial local copy.
    ///
    ///     <para>
    ///     When a tool wants confirming the conversation stops with <see cref="Pending"/> set and
    ///     waits. Nothing has happened at that point. It carries on through
    ///     <see cref="ResolveAsync"/>, either way: a refusal is sent back to the model as a refusal
    ///     rather than dropped, so it can say what it would have done instead of appearing to have
    ///     done it.
    ///     </para>
    ///
    ///     <para>
    ///     Not thread safe, and not meant to be. One of these belongs to one person, who is
    ///     assumed to be asking one question at a time. Two tabs open on the same account share it,
    ///     which is the honest reading of carrying on where you left off, and would be the case on
    ///     the other side regardless: the real history sits at OpenAI against one response id.
    ///     </para>
    /// </remarks>
    public sealed class AiConversation
    {
        private readonly IAiService ai;
        private readonly string instructions;
        private readonly IReadOnlyList<AiTool> tools;
        private readonly int? maxOutputTokens;

        private string previousResponseId;

        public AiConversation(
            IAiService ai,
            string instructions,
            IReadOnlyList<AiTool> tools = null,
            int? maxOutputTokens = null)
        {
            this.ai = ai;
            this.instructions = instructions;
            this.tools = tools;
            this.maxOutputTokens = maxOutputTokens;
        }

        /// <summary>
        ///     How many turns are kept for display. The model's own history is not affected: it
        ///     lives at OpenAI against the response id, so trimming here only stops a very long
        ///     exchange growing without limit in memory.
        /// </summary>
        private const int MaxRememberedTurns = 200;

        public List<AiTurn> Turns { get; } = new List<AiTurn>();

        /// <summary>Set while something is waiting to be agreed to. Nothing has run.</summary>
        public AiPendingAction Pending { get; private set; }

        /// <summary>The last thing that went wrong, written for the person reading it.</summary>
        public string Error { get; private set; }

        public bool IsWaiting => Pending != null;

        /// <param name="context">
        ///     Where the person is and what they are looking at, if that is known. Sent with the
        ///     question rather than folded into the instructions, because one conversation outlives
        ///     any one page: the instructions are fixed when the conversation starts, and by the
        ///     third question the reader may be somewhere else entirely.
        /// </param>
        public async Task AskAsync(string text, string context = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            // A new question while something is waiting would leave the pending call unanswered on
            // the other side, and the model would carry on as though it had been agreed to.
            if (IsWaiting) return;

            Error = null;

            // Only the question is kept as a turn. The context is scaffolding for the model and
            // showing it back would read as though the person had typed it.
            Remember(new AiTurn { FromUser = true, Text = text });

            var input = string.IsNullOrWhiteSpace(context)
                ? text
                : "[Context, not part of the question: " + context + "]" + Environment.NewLine
                  + Environment.NewLine + text;

            var result = await ai.AskAsync(new AiRequest
            {
                Instructions = instructions,
                Input = input,
                Tools = tools,
                MaxOutputTokens = maxOutputTokens,
                PreviousResponseId = previousResponseId
            }, cancellationToken);

            Absorb(result);
        }

        /// <summary>
        ///     Answers the question the assistant is waiting on. The only path by which a tool that
        ///     needs confirming ever runs.
        /// </summary>
        public async Task ResolveAsync(bool approved, CancellationToken cancellationToken = default)
        {
            var pending = Pending;
            if (pending == null) return;

            // Cleared before the call rather than after, so a slow or failed round trip cannot leave
            // the same confirmation on screen to be pressed a second time.
            Pending = null;
            Error = null;

            var result = await ai.ContinueAsync(pending, approved, new AiRequest
            {
                Instructions = instructions,
                Tools = tools,
                MaxOutputTokens = maxOutputTokens
            }, cancellationToken);

            Absorb(result);
        }

        private void Absorb(AiResult result)
        {
            if (result == null)
            {
                Error = "No answer came back. Try again.";
                return;
            }

            if (!string.IsNullOrWhiteSpace(result.ResponseId))
            {
                previousResponseId = result.ResponseId;
            }

            if (result.Pending != null)
            {
                Pending = result.Pending;
                return;
            }

            if (result.Error != null)
            {
                Error = result.Error;
                return;
            }

            if (!string.IsNullOrWhiteSpace(result.Text))
            {
                Remember(new AiTurn { FromUser = false, Text = result.Text });
            }
        }

        private void Remember(AiTurn turn)
        {
            Turns.Add(turn);

            if (Turns.Count > MaxRememberedTurns)
            {
                Turns.RemoveRange(0, Turns.Count - MaxRememberedTurns);
            }
        }
    }
}
