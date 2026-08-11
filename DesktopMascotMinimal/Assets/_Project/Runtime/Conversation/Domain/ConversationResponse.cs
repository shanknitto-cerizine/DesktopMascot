namespace DesktopMascot.Runtime.Conversation.Domain
{
    internal enum ConversationPresentationIntent
    {
        CharacterUtterance = 1
    }

    internal sealed class ConversationResponse
    {
        internal const int MaximumTextLength = 1024;

        private ConversationResponse(
            ConversationRequestId requestId,
            ConversationLogicalId responseId,
            ConversationLogicalId speakerId,
            string text,
            ConversationPresentationIntent presentationIntent)
        {
            RequestId = requestId;
            ResponseId = responseId;
            SpeakerId = speakerId;
            Text = text;
            PresentationIntent = presentationIntent;
        }

        internal ConversationRequestId RequestId { get; }
        internal ConversationLogicalId ResponseId { get; }
        internal ConversationLogicalId SpeakerId { get; }
        internal string Text { get; }
        internal ConversationPresentationIntent PresentationIntent { get; }

        internal static bool TryCreate(
            ConversationRequestId requestId,
            ConversationLogicalId responseId,
            ConversationLogicalId speakerId,
            string text,
            ConversationPresentationIntent presentationIntent,
            out ConversationResponse response,
            out ConversationContractFailure failure)
        {
            response = null;
            failure = ConversationContractFailure.None;
            if (!requestId.IsValid)
            {
                failure = ConversationContractFailure.InvalidRequestId;
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

            if (!TryValidateText(text, out failure))
                return false;

            if (!IsSupportedPresentationIntent(presentationIntent))
            {
                failure = ConversationContractFailure
                    .UnsupportedPresentationIntent;
                return false;
            }

            response = new ConversationResponse(
                requestId,
                responseId,
                speakerId,
                text,
                presentationIntent);
            return true;
        }

        internal static bool TryValidateText(
            string text,
            out ConversationContractFailure failure)
        {
            failure = ConversationContractFailure.None;
            if (text == null)
            {
                failure = ConversationContractFailure.MissingRequiredValue;
                return false;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                failure = ConversationContractFailure.EmptyText;
                return false;
            }

            if (text.Length > MaximumTextLength)
            {
                failure = ConversationContractFailure.TextTooLong;
                return false;
            }

            for (var index = 0; index < text.Length; ++index)
            {
                var character = text[index];
                if (char.IsHighSurrogate(character))
                {
                    if (index + 1 >= text.Length
                        || !char.IsLowSurrogate(text[index + 1]))
                    {
                        failure = ConversationContractFailure.InvalidText;
                        return false;
                    }

                    ++index;
                    continue;
                }

                if (char.IsLowSurrogate(character)
                    || (char.IsControl(character)
                        && character != '\r'
                        && character != '\n'
                        && character != '\t'))
                {
                    failure = ConversationContractFailure.InvalidText;
                    return false;
                }
            }

            return true;
        }

        internal static bool IsSupportedPresentationIntent(
            ConversationPresentationIntent presentationIntent)
        {
            return presentationIntent
                == ConversationPresentationIntent.CharacterUtterance;
        }
    }
}
