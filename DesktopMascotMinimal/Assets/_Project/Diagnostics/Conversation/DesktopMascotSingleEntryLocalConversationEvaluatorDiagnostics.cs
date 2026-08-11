using System;
using DesktopMascot.Runtime.Conversation.Domain;
using DesktopMascot.Runtime.Conversation.Evaluation;

namespace DesktopMascot.Diagnostics.Conversation
{
    public static class DesktopMascotSingleEntryLocalConversationEvaluatorDiagnostics
    {
        public static bool Run(out string failure)
        {
            failure = null;
            try
            {
                VerifyMatchingTriggerProducesEntryResponse();
                VerifyNonMatchingTriggerProducesNoMatch();
                VerifyCharacterIdDoesNotAffectMatching();
                VerifyRepeatedEvaluationIsDeterministic();
                VerifyInputsRemainUnchanged();
                VerifyNullEntryIsRejected();
                return true;
            }
            catch (InvalidOperationException exception)
            {
                failure = exception.Message;
                return false;
            }
        }

        private static void VerifyMatchingTriggerProducesEntryResponse()
        {
            var entry = CreateEntry("trigger.match");
            var request = CreateRequest(
                "request.match", "trigger.match", "character.first");
            ILocalConversationEvaluator evaluator =
                new SingleEntryLocalConversationEvaluator(entry);
            var result = evaluator.Evaluate(request);

            Require(
                result != null
                && result.Outcome == ConversationEvaluationOutcome.ResponseProduced
                && result.RequestId == request.RequestId
                && result.Response != null
                && result.Response.RequestId == request.RequestId
                && result.Response.ResponseId == entry.ResponseId
                && result.Response.SpeakerId == entry.SpeakerId
                && result.Response.Text == entry.Text
                && result.Response.PresentationIntent == entry.PresentationIntent,
                "Matching trigger did not produce the correlated entry response.");
        }

        private static void VerifyNonMatchingTriggerProducesNoMatch()
        {
            var entry = CreateEntry("trigger.entry");
            var request = CreateRequest(
                "request.no-match", "trigger.request", "character.first");
            ILocalConversationEvaluator evaluator =
                new SingleEntryLocalConversationEvaluator(entry);
            var result = evaluator.Evaluate(request);

            Require(
                result != null
                && result.Outcome == ConversationEvaluationOutcome.NoMatch
                && result.RequestId == request.RequestId
                && result.Response == null,
                "Non-matching trigger did not produce correlated NoMatch.");
        }

        private static void VerifyCharacterIdDoesNotAffectMatching()
        {
            var entry = CreateEntry("trigger.character-independent");
            ILocalConversationEvaluator evaluator =
                new SingleEntryLocalConversationEvaluator(entry);
            var firstRequest = CreateRequest(
                "request.character.first",
                "trigger.character-independent",
                "character.first");
            var secondRequest = CreateRequest(
                "request.character.second",
                "trigger.character-independent",
                "character.second");
            var firstResult = evaluator.Evaluate(firstRequest);
            var secondResult = evaluator.Evaluate(secondRequest);

            Require(
                firstResult.Outcome == ConversationEvaluationOutcome.ResponseProduced
                && secondResult.Outcome
                    == ConversationEvaluationOutcome.ResponseProduced
                && firstResult.Response.ResponseId == entry.ResponseId
                && secondResult.Response.ResponseId == entry.ResponseId,
                "Character ID affected TriggerId-only matching.");
        }

        private static void VerifyRepeatedEvaluationIsDeterministic()
        {
            var entry = CreateEntry("trigger.repeat");
            ILocalConversationEvaluator evaluator =
                new SingleEntryLocalConversationEvaluator(entry);
            var matchingRequest = CreateRequest(
                "request.repeat.match", "trigger.repeat", "character.first");
            var nonMatchingRequest = CreateRequest(
                "request.repeat.no-match", "trigger.other", "character.first");
            var firstMatch = evaluator.Evaluate(matchingRequest);
            var secondMatch = evaluator.Evaluate(matchingRequest);
            var firstNoMatch = evaluator.Evaluate(nonMatchingRequest);
            var secondNoMatch = evaluator.Evaluate(nonMatchingRequest);

            Require(
                firstMatch.Outcome == secondMatch.Outcome
                && firstMatch.RequestId == secondMatch.RequestId
                && firstMatch.Response != null
                && secondMatch.Response != null
                && firstMatch.Response.ResponseId == secondMatch.Response.ResponseId
                && firstMatch.Response.SpeakerId == secondMatch.Response.SpeakerId
                && firstMatch.Response.Text == secondMatch.Response.Text
                && firstMatch.Response.PresentationIntent
                    == secondMatch.Response.PresentationIntent,
                "Repeated matching evaluation was not deterministic.");
            Require(
                firstNoMatch.Outcome == secondNoMatch.Outcome
                && firstNoMatch.RequestId == secondNoMatch.RequestId
                && firstNoMatch.Response == null
                && secondNoMatch.Response == null,
                "Repeated non-matching evaluation was not deterministic.");
        }

        private static void VerifyInputsRemainUnchanged()
        {
            var entry = CreateEntry("trigger.unchanged");
            var request = CreateRequest(
                "request.unchanged", "trigger.unchanged", "character.unchanged");
            var triggerBefore = entry.TriggerId;
            var responseBefore = entry.ResponseId;
            var speakerBefore = entry.SpeakerId;
            var textBefore = entry.Text;
            var intentBefore = entry.PresentationIntent;
            var requestIdBefore = request.RequestId;
            var eventTriggerBefore = request.Event.TriggerId;
            var characterBefore = request.CharacterId;

            new SingleEntryLocalConversationEvaluator(entry).Evaluate(request);

            Require(
                entry.TriggerId == triggerBefore
                && entry.ResponseId == responseBefore
                && entry.SpeakerId == speakerBefore
                && entry.Text == textBefore
                && entry.PresentationIntent == intentBefore
                && request.RequestId == requestIdBefore
                && request.Event.TriggerId == eventTriggerBefore
                && request.CharacterId == characterBefore,
                "Evaluation changed its request or entry.");
        }

        private static void VerifyNullEntryIsRejected()
        {
            try
            {
                new SingleEntryLocalConversationEvaluator(null);
            }
            catch (ArgumentNullException exception)
            {
                Require(exception.ParamName == "entry",
                    "Null entry rejection used the wrong parameter name.");
                return;
            }

            throw new InvalidOperationException(
                "Null entry construction was not rejected.");
        }

        private static LocalConversationResponseEntry CreateEntry(string triggerValue)
        {
            Require(LocalConversationResponseEntry.TryCreate(
                    CreateLogicalId(triggerValue),
                    CreateLogicalId("response.entry"),
                    CreateLogicalId("speaker.entry"),
                    "Entry response text.",
                    ConversationPresentationIntent.CharacterUtterance,
                    out var entry,
                    out var failure)
                && failure == ConversationContractFailure.None,
                "Valid local response entry was rejected.");
            return entry;
        }

        private static ConversationRequest CreateRequest(
            string requestValue,
            string triggerValue,
            string characterValue)
        {
            Require(ConversationRequestId.TryCreate(requestValue, out var requestId),
                "Valid request ID was rejected.");
            Require(ConversationEvent.TryCreate(
                    CreateLogicalId(triggerValue),
                    out var conversationEvent,
                    out var eventFailure)
                && eventFailure == ConversationContractFailure.None,
                "Valid conversation event was rejected.");
            Require(ConversationRequest.TryCreate(
                    requestId,
                    conversationEvent,
                    CreateLogicalId(characterValue),
                    out var request,
                    out var requestFailure)
                && requestFailure == ConversationContractFailure.None,
                "Valid conversation request was rejected.");
            return request;
        }

        private static ConversationLogicalId CreateLogicalId(string value)
        {
            Require(ConversationLogicalId.TryCreate(value, out var logicalId),
                "Valid logical ID was rejected.");
            return logicalId;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
