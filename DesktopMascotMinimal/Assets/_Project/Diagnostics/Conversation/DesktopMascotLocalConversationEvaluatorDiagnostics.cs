using System;
using DesktopMascot.Runtime.Conversation.Domain;
using DesktopMascot.Runtime.Conversation.Evaluation;

namespace DesktopMascot.Diagnostics.Conversation
{
    public static class DesktopMascotLocalConversationEvaluatorDiagnostics
    {
        public static bool Run(out string failure)
        {
            failure = null;
            try
            {
                VerifyNoMatch();
                VerifyResponseProduced();
                VerifyRepeatedEvaluationIsDeterministic();
                return true;
            }
            catch (InvalidOperationException exception)
            {
                failure = exception.Message;
                return false;
            }
        }

        private static void VerifyNoMatch()
        {
            var request = CreateRequest("local-evaluator.request.no-match");
            ILocalConversationEvaluator evaluator = new NoMatchEvaluator();
            var result = evaluator.Evaluate(request);

            Require(
                result != null
                && result.Outcome == ConversationEvaluationOutcome.NoMatch
                && result.RequestId == request.RequestId
                && result.Response == null,
                "NoMatch evaluation was not correlated or contained a response.");
            RequireUnchanged(request, "NoMatch evaluation mutated its request.");
        }

        private static void VerifyResponseProduced()
        {
            var request = CreateRequest("local-evaluator.request.response");
            ILocalConversationEvaluator evaluator = new ResponseProducedEvaluator();
            var result = evaluator.Evaluate(request);

            Require(
                result != null
                && result.Outcome == ConversationEvaluationOutcome.ResponseProduced
                && result.RequestId == request.RequestId
                && result.Response != null
                && result.Response.RequestId == request.RequestId,
                "ResponseProduced evaluation was not correlated.");
            RequireUnchanged(request, "ResponseProduced evaluation mutated its request.");
        }

        private static void VerifyRepeatedEvaluationIsDeterministic()
        {
            var request = CreateRequest("local-evaluator.request.repeat");
            ILocalConversationEvaluator noMatch = new NoMatchEvaluator();
            ILocalConversationEvaluator responseProduced =
                new ResponseProducedEvaluator();
            var firstNoMatch = noMatch.Evaluate(request);
            var secondNoMatch = noMatch.Evaluate(request);
            var firstResponse = responseProduced.Evaluate(request);
            var secondResponse = responseProduced.Evaluate(request);

            Require(
                firstNoMatch != null
                && secondNoMatch != null
                && firstNoMatch.Outcome == secondNoMatch.Outcome
                && firstNoMatch.RequestId == secondNoMatch.RequestId
                && firstNoMatch.Response == null
                && secondNoMatch.Response == null,
                "Repeated NoMatch evaluation was not deterministic.");
            Require(
                firstResponse != null
                && secondResponse != null
                && firstResponse.Outcome == secondResponse.Outcome
                && firstResponse.RequestId == secondResponse.RequestId
                && firstResponse.Response != null
                && secondResponse.Response != null
                && firstResponse.Response.RequestId == secondResponse.Response.RequestId
                && firstResponse.Response.ResponseId == secondResponse.Response.ResponseId
                && firstResponse.Response.SpeakerId == secondResponse.Response.SpeakerId
                && firstResponse.Response.Text == secondResponse.Response.Text
                && firstResponse.Response.PresentationIntent
                    == secondResponse.Response.PresentationIntent,
                "Repeated ResponseProduced evaluation was not deterministic.");
            RequireUnchanged(request, "Repeated evaluation mutated its request.");
        }

        private static ConversationRequest CreateRequest(string requestValue)
        {
            Require(ConversationRequestId.TryCreate(requestValue, out var requestId),
                "Valid request ID was rejected.");
            Require(ConversationLogicalId.TryCreate(
                    ConversationTriggerIds.ExplicitRequest,
                    out var triggerId),
                "Valid trigger ID was rejected.");
            Require(ConversationEvent.TryCreate(
                    triggerId,
                    out var conversationEvent,
                    out var eventFailure)
                && eventFailure == ConversationContractFailure.None,
                "Valid event was rejected.");
            Require(ConversationLogicalId.TryCreate("character.diagnostic", out var characterId),
                "Valid character ID was rejected.");
            Require(ConversationRequest.TryCreate(
                    requestId,
                    conversationEvent,
                    characterId,
                    out var request,
                    out var requestFailure)
                && requestFailure == ConversationContractFailure.None,
                "Valid request was rejected.");
            return request;
        }

        private static void RequireUnchanged(ConversationRequest request, string message)
        {
            Require(
                request.Event.TriggerId.Value == ConversationTriggerIds.ExplicitRequest
                && request.CharacterId.Value == "character.diagnostic",
                message);
        }

        private static ConversationEvaluationResult CreateNoMatch(
            ConversationRequest request)
        {
            Require(ConversationEvaluationResult.TryCreate(
                    ConversationEvaluationOutcome.NoMatch,
                    request.RequestId,
                    null,
                    out var result,
                    out var failure)
                && failure == ConversationEvaluationContractFailure.None,
                "Diagnostic NoMatch result creation failed.");
            return result;
        }

        private static ConversationEvaluationResult CreateResponseProduced(
            ConversationRequest request)
        {
            Require(ConversationLogicalId.TryCreate("response.diagnostic", out var responseId),
                "Valid response ID was rejected.");
            Require(ConversationLogicalId.TryCreate("speaker.diagnostic", out var speakerId),
                "Valid speaker ID was rejected.");
            Require(ConversationResponse.TryCreate(
                    request.RequestId,
                    responseId,
                    speakerId,
                    "Diagnostic response.",
                    ConversationPresentationIntent.CharacterUtterance,
                    out var response,
                    out var responseFailure)
                && responseFailure == ConversationContractFailure.None,
                "Diagnostic response creation failed.");
            Require(ConversationEvaluationResult.TryCreate(
                    ConversationEvaluationOutcome.ResponseProduced,
                    request.RequestId,
                    response,
                    out var result,
                    out var resultFailure)
                && resultFailure == ConversationEvaluationContractFailure.None,
                "Diagnostic ResponseProduced result creation failed.");
            return result;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private sealed class NoMatchEvaluator : ILocalConversationEvaluator
        {
            public ConversationEvaluationResult Evaluate(ConversationRequest request)
            {
                return CreateNoMatch(request);
            }
        }

        private sealed class ResponseProducedEvaluator : ILocalConversationEvaluator
        {
            public ConversationEvaluationResult Evaluate(ConversationRequest request)
            {
                return CreateResponseProduced(request);
            }
        }
    }
}
