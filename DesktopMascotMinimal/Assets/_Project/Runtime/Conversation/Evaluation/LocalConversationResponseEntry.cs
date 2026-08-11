using DesktopMascot.Runtime.Conversation.Domain;

namespace DesktopMascot.Runtime.Conversation.Evaluation
{
    // Immutable local response material; request correlation belongs to a future evaluator.
    internal sealed class LocalConversationResponseEntry
    {
        private LocalConversationResponseEntry(
            ConversationLogicalId triggerId,
            ConversationLogicalId responseId,
            ConversationLogicalId speakerId,
            string text,
            ConversationPresentationIntent presentationIntent)
        {
            TriggerId = triggerId;
            ResponseId = responseId;
            SpeakerId = speakerId;
            Text = text;
            PresentationIntent = presentationIntent;
        }

        internal ConversationLogicalId TriggerId { get; }
        internal ConversationLogicalId ResponseId { get; }
        internal ConversationLogicalId SpeakerId { get; }
        internal string Text { get; }
        internal ConversationPresentationIntent PresentationIntent { get; }

        internal static bool TryCreate(
            ConversationLogicalId triggerId,
            ConversationLogicalId responseId,
            ConversationLogicalId speakerId,
            string text,
            ConversationPresentationIntent presentationIntent,
            out LocalConversationResponseEntry entry,
            out ConversationContractFailure failure)
        {
            entry = null;
            failure = ConversationContractFailure.None;
            if (!triggerId.IsValid)
            {
                failure = ConversationContractFailure.InvalidTriggerId;
                return false;
            }

            if (!responseId.IsValid)
            {
                failure = ConversationContractFailure.InvalidResponseId;
                return false;
            }

            if (!speakerId.IsValid)
            {
                failure = ConversationContractFailure.InvalidSpeakerId;
                return false;
            }

            if (!ConversationResponse.TryValidateText(text, out failure))
                return false;

            if (!ConversationResponse.IsSupportedPresentationIntent(
                    presentationIntent))
            {
                failure = ConversationContractFailure
                    .UnsupportedPresentationIntent;
                return false;
            }

            entry = new LocalConversationResponseEntry(
                triggerId,
                responseId,
                speakerId,
                text,
                presentationIntent);
            return true;
        }
    }
}
