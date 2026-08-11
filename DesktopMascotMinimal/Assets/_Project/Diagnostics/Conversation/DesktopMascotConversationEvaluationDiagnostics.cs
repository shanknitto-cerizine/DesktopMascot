using System;
using DesktopMascot.Runtime.Conversation.Domain;
using DesktopMascot.Runtime.Conversation.Evaluation;

namespace DesktopMascot.Diagnostics.Conversation
{
    public static class DesktopMascotConversationEvaluationDiagnostics
    {
        public static bool Run(out string failure)
        {
            failure = null;
            try
            {
                VerifyResponseProduced();
                VerifyNoMatch();
                VerifyExpectedFailures();
                VerifyDeterministicConstruction();
                return true;
            }
            catch (InvalidOperationException exception)
            {
                failure = exception.Message;
                return false;
            }
        }

        private static void VerifyResponseProduced()
        {
            var requestId = CreateRequestId("evaluation.request.001");
            var response = CreateResponse(requestId, "response.001");
            Require(
                ConversationEvaluationResult.TryCreate(
                    ConversationEvaluationOutcome.ResponseProduced,
                    requestId,
                    response,
                    out var result,
                    out var failure)
                && failure == ConversationEvaluationContractFailure.None
                && result.Outcome == ConversationEvaluationOutcome.ResponseProduced
                && result.RequestId == requestId
                && result.Response == response,
                "ResponseProduced result was rejected or changed its values.");
        }

        private static void VerifyNoMatch()
        {
            var requestId = CreateRequestId("evaluation.request.002");
            Require(
                ConversationEvaluationResult.TryCreate(
                    ConversationEvaluationOutcome.NoMatch,
                    requestId,
                    null,
                    out var result,
                    out var failure)
                && failure == ConversationEvaluationContractFailure.None
                && result.Outcome == ConversationEvaluationOutcome.NoMatch
                && result.RequestId == requestId
                && result.Response == null,
                "NoMatch result was rejected or contains a response.");
        }

        private static void VerifyExpectedFailures()
        {
            var requestId = CreateRequestId("evaluation.request.003");
            var otherRequestId = CreateRequestId("evaluation.request.004");
            var response = CreateResponse(requestId, "response.003");
            var otherResponse = CreateResponse(otherRequestId, "response.004");

            RequireFailure(
                ConversationEvaluationOutcome.ResponseProduced,
                requestId,
                null,
                ConversationEvaluationContractFailure.MissingResponse,
                "ResponseProduced missing response");
            RequireFailure(
                ConversationEvaluationOutcome.ResponseProduced,
                requestId,
                otherResponse,
                ConversationEvaluationContractFailure.MismatchedResponseRequestId,
                "mismatched request ID");
            RequireFailure(
                ConversationEvaluationOutcome.NoMatch,
                requestId,
                response,
                ConversationEvaluationContractFailure.UnexpectedResponse,
                "NoMatch response");
            RequireFailure(
                ConversationEvaluationOutcome.NoMatch,
                default,
                null,
                ConversationEvaluationContractFailure.InvalidRequestId,
                "default request ID");
            RequireFailure(
                (ConversationEvaluationOutcome)99,
                requestId,
                null,
                ConversationEvaluationContractFailure.UnsupportedOutcome,
                "unsupported outcome");
        }

        private static void VerifyDeterministicConstruction()
        {
            var requestId = CreateRequestId("evaluation.request.005");
            var response = CreateResponse(requestId, "response.005");
            Require(
                ConversationEvaluationResult.TryCreate(
                    ConversationEvaluationOutcome.ResponseProduced,
                    requestId,
                    response,
                    out var first,
                    out var firstFailure)
                && ConversationEvaluationResult.TryCreate(
                    ConversationEvaluationOutcome.ResponseProduced,
                    requestId,
                    response,
                    out var second,
                    out var secondFailure)
                && firstFailure == ConversationEvaluationContractFailure.None
                && secondFailure == firstFailure
                && first.Outcome == second.Outcome
                && first.RequestId == second.RequestId
                && first.Response == second.Response,
                "Repeated result construction was not deterministic.");
        }

        private static ConversationRequestId CreateRequestId(string value)
        {
            Require(ConversationRequestId.TryCreate(value, out var requestId),
                "Valid request ID was rejected.");
            return requestId;
        }

        private static ConversationResponse CreateResponse(
            ConversationRequestId requestId,
            string responseValue)
        {
            Require(ConversationLogicalId.TryCreate(responseValue, out var responseId),
                "Valid response ID was rejected.");
            Require(ConversationLogicalId.TryCreate("speaker.mascot", out var speakerId),
                "Valid speaker ID was rejected.");
            Require(
                ConversationResponse.TryCreate(
                    requestId,
                    responseId,
                    speakerId,
                    "Response text.",
                    ConversationPresentationIntent.CharacterUtterance,
                    out var response,
                    out var failure)
                && failure == ConversationContractFailure.None,
                "Valid response was rejected.");
            return response;
        }

        private static void RequireFailure(
            ConversationEvaluationOutcome outcome,
            ConversationRequestId requestId,
            ConversationResponse response,
            ConversationEvaluationContractFailure expectedFailure,
            string description)
        {
            Require(
                !ConversationEvaluationResult.TryCreate(
                    outcome,
                    requestId,
                    response,
                    out var result,
                    out var failure)
                && result == null
                && failure == expectedFailure,
                "Invalid " + description + " did not report expected failure.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
