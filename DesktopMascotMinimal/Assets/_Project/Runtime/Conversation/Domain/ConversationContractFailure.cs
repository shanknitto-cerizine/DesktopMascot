namespace DesktopMascot.Runtime.Conversation.Domain
{
    // Stable, non-localized diagnostics for expected contract validation.
    internal enum ConversationContractFailure
    {
        None = 0,
        MissingRequiredValue = 1,
        InvalidRequestId = 2,
        InvalidTriggerId = 3,
        InvalidCharacterId = 4,
        InvalidResponseId = 5,
        InvalidSpeakerId = 6,
        EmptyText = 7,
        TextTooLong = 8,
        InvalidText = 9,
        UnsupportedPresentationIntent = 10
    }
}
