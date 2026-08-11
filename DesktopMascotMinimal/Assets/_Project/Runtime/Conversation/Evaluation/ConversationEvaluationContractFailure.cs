namespace DesktopMascot.Runtime.Conversation.Evaluation
{
    // Stable, non-localized diagnostics for expected result validation.
    internal enum ConversationEvaluationContractFailure
    {
        None = 0,
        InvalidRequestId = 1,
        UnsupportedOutcome = 2,
        MissingResponse = 3,
        MismatchedResponseRequestId = 4,
        UnexpectedResponse = 5
    }
}
