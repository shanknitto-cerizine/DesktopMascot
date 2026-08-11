using System;
using DesktopMascot.Runtime.Conversation.Domain;

namespace DesktopMascot.Runtime.Conversation.Evaluation
{
    // Production-usable, single-entry local evaluator; runtime wiring is separate.
    internal sealed class SingleEntryLocalConversationEvaluator :
        ILocalConversationEvaluator
    {
        private readonly LocalConversationResponseEntry entry;

        internal SingleEntryLocalConversationEvaluator(
            LocalConversationResponseEntry entry)
        {
            this.entry = entry ?? throw new ArgumentNullException(nameof(entry));
        }

        public ConversationEvaluationResult Evaluate(ConversationRequest request)
        {
            if (request.Event.TriggerId != entry.TriggerId)
                return CreateNoMatch(request);

            if (!ConversationResponse.TryCreate(
                    request.RequestId,
                    entry.ResponseId,
                    entry.SpeakerId,
                    entry.Text,
                    entry.PresentationIntent,
                    out var response,
                    out _))
            {
                throw new InvalidOperationException(
                    "Validated local evaluator response construction failed.");
            }

            if (!ConversationEvaluationResult.TryCreate(
                    ConversationEvaluationOutcome.ResponseProduced,
                    request.RequestId,
                    response,
                    out var result,
                    out _))
            {
                throw new InvalidOperationException(
                    "Validated local evaluator result construction failed.");
            }

            return result;
        }

        private static ConversationEvaluationResult CreateNoMatch(
            ConversationRequest request)
        {
            if (!ConversationEvaluationResult.TryCreate(
                    ConversationEvaluationOutcome.NoMatch,
                    request.RequestId,
                    null,
                    out var result,
                    out _))
            {
                throw new InvalidOperationException(
                    "Validated local evaluator NoMatch construction failed.");
            }

            return result;
        }
    }
}
