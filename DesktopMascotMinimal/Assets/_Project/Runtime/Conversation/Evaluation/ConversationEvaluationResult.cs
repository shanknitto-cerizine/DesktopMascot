using DesktopMascot.Runtime.Conversation.Domain;

namespace DesktopMascot.Runtime.Conversation.Evaluation
{
    internal sealed class ConversationEvaluationResult
    {
        private ConversationEvaluationResult(
            ConversationEvaluationOutcome outcome,
            ConversationRequestId requestId,
            ConversationResponse response)
        {
            Outcome = outcome;
            RequestId = requestId;
            Response = response;
        }

        internal ConversationEvaluationOutcome Outcome { get; }
        internal ConversationRequestId RequestId { get; }
        internal ConversationResponse Response { get; }

        internal static bool TryCreate(
            ConversationEvaluationOutcome outcome,
            ConversationRequestId requestId,
            ConversationResponse response,
            out ConversationEvaluationResult result,
            out ConversationEvaluationContractFailure failure)
        {
            result = null;
            failure = ConversationEvaluationContractFailure.None;
            if (!requestId.IsValid)
            {
                failure = ConversationEvaluationContractFailure.InvalidRequestId;
                return false;
            }

            switch (outcome)
            {
                case ConversationEvaluationOutcome.ResponseProduced:
                    if (response == null)
                    {
                        failure = ConversationEvaluationContractFailure
                            .MissingResponse;
                        return false;
                    }

                    if (response.RequestId != requestId)
                    {
                        failure = ConversationEvaluationContractFailure
                            .MismatchedResponseRequestId;
                        return false;
                    }

                    break;

                case ConversationEvaluationOutcome.NoMatch:
                    if (response != null)
                    {
                        failure = ConversationEvaluationContractFailure
                            .UnexpectedResponse;
                        return false;
                    }

                    break;

                default:
                    failure = ConversationEvaluationContractFailure
                        .UnsupportedOutcome;
                    return false;
            }

            result = new ConversationEvaluationResult(outcome, requestId, response);
            return true;
        }
    }
}
