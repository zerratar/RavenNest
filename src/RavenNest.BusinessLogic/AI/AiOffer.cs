using System;

namespace RavenNest.BusinessLogic.AI
{
    public enum AiOfferKind
    {
        /// <summary>Takes the person to a page. Changes nothing, so it needs no confirming.</summary>
        Navigate
    }

    /// <summary>
    ///     Something the assistant is offering alongside its answer, drawn as a button.
    /// </summary>
    /// <remarks>
    ///     An answer that says "you can do that on your stash page" is a worse answer than one with
    ///     a button on it that goes there. This is what turns the chat from something that describes
    ///     the site into something you can act through.
    ///
    ///     <para>
    ///     Deliberately not the same thing as a confirmation. A confirmation is the assistant asking
    ///     permission to do something itself, and it blocks the conversation until it is answered.
    ///     An offer is optional, several can sit side by side, and ignoring them costs nothing.
    ///     Navigation is the first kind because it cannot go wrong; anything that changes something
    ///     still goes through the confirmation gate.
    ///     </para>
    /// </remarks>
    public sealed class AiOffer
    {
        public AiOffer(AiOfferKind kind, string label, string target)
        {
            Kind = kind;
            Label = label;
            Target = target;
        }

        public AiOfferKind Kind { get; }

        /// <summary>What the button says. Written from the person's point of view.</summary>
        public string Label { get; }

        /// <summary>For navigation, the path to go to.</summary>
        public string Target { get; }
    }
}
