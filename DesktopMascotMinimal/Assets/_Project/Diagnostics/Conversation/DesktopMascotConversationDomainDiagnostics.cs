using System;
using DesktopMascot.Runtime.Conversation.Domain;

namespace DesktopMascot.Diagnostics.Conversation
{
    public static class DesktopMascotConversationDomainDiagnostics
    {
        public static bool Run(out string failure)
        {
            failure = null;
            try
            {
                VerifyBuiltInTriggers();
                VerifyEventAndRequestValidation();
                VerifyResponseValidation();
                VerifyIdentifierSemantics();
                return true;
            }
            catch (InvalidOperationException exception)
            {
                failure = exception.Message;
                return false;
            }
        }

        private static void VerifyBuiltInTriggers()
        {
            VerifyLogicalId(ConversationTriggerIds.MascotClick, "mascot click");
            VerifyLogicalId(ConversationTriggerIds.DragStart, "drag start");
            VerifyLogicalId(ConversationTriggerIds.DragEnd, "drag end");
            VerifyLogicalId(ConversationTriggerIds.Startup, "startup");
            VerifyLogicalId(ConversationTriggerIds.Idle, "idle");
            VerifyLogicalId(ConversationTriggerIds.TimeBased, "time based");
            VerifyLogicalId(
                ConversationTriggerIds.ExplicitRequest,
                "explicit request");
        }

        private static void VerifyEventAndRequestValidation()
        {
            Require(
                ConversationLogicalId.TryCreate(
                    ConversationTriggerIds.MascotClick,
                    out var triggerId),
                "Valid trigger ID was rejected.");
            Require(
                ConversationEvent.TryCreate(
                    triggerId,
                    out var conversationEvent,
                    out var failure)
                && failure == ConversationContractFailure.None,
                "Valid event was rejected.");
            Require(
                !ConversationEvent.TryCreate(
                    default,
                    out _,
                    out failure)
                && failure == ConversationContractFailure.InvalidTriggerId,
                "Invalid trigger did not report InvalidTriggerId.");

            Require(
                ConversationRequestId.TryCreate("request.001", out var requestId),
                "Valid request ID was rejected.");
            Require(
                ConversationLogicalId.TryCreate(
                    "desktop-mascot.bundled-default",
                    out var characterId),
                "Valid character ID was rejected.");
            Require(
                ConversationRequest.TryCreate(
                    requestId,
                    conversationEvent,
                    characterId,
                    out _,
                    out failure)
                && failure == ConversationContractFailure.None,
                "Valid request was rejected.");
            Require(
                !ConversationRequest.TryCreate(
                    requestId,
                    null,
                    characterId,
                    out _,
                    out failure)
                && failure == ConversationContractFailure.MissingRequiredValue,
                "Missing event did not report MissingRequiredValue.");
            Require(
                !ConversationRequest.TryCreate(
                    default,
                    conversationEvent,
                    characterId,
                    out _,
                    out failure)
                && failure == ConversationContractFailure.InvalidRequestId,
                "Invalid request ID did not report InvalidRequestId.");
            Require(
                !ConversationRequest.TryCreate(
                    requestId,
                    conversationEvent,
                    default,
                    out _,
                    out failure)
                && failure == ConversationContractFailure.InvalidCharacterId,
                "Invalid character ID did not report InvalidCharacterId.");
        }

        private static void VerifyResponseValidation()
        {
            Require(
                ConversationRequestId.TryCreate("request.002", out var requestId),
                "Valid response request ID was rejected.");
            Require(
                ConversationLogicalId.TryCreate("response.greeting", out var responseId),
                "Valid response ID was rejected.");
            Require(
                ConversationLogicalId.TryCreate("speaker.mascot", out var speakerId),
                "Valid speaker ID was rejected.");
            Require(
                ConversationResponse.TryCreate(
                    requestId,
                    responseId,
                    speakerId,
                    "Hello\nthere.",
                    ConversationPresentationIntent.CharacterUtterance,
                    out var response,
                    out var failure)
                && response.RequestId == requestId
                && failure == ConversationContractFailure.None,
                "Valid response or request-response correlation failed.");
            Require(
                !ConversationResponse.TryCreate(
                    requestId,
                    default,
                    speakerId,
                    "Hello",
                    ConversationPresentationIntent.CharacterUtterance,
                    out _,
                    out failure)
                && failure == ConversationContractFailure.InvalidResponseId,
                "Invalid response ID did not report InvalidResponseId.");
            Require(
                !ConversationResponse.TryCreate(
                    requestId,
                    responseId,
                    default,
                    "Hello",
                    ConversationPresentationIntent.CharacterUtterance,
                    out _,
                    out failure)
                && failure == ConversationContractFailure.InvalidSpeakerId,
                "Invalid speaker ID did not report InvalidSpeakerId.");
            RequireInvalidText(requestId, responseId, speakerId, string.Empty,
                ConversationContractFailure.EmptyText, "empty text");
            RequireInvalidText(requestId, responseId, speakerId, " \t\r\n",
                ConversationContractFailure.EmptyText, "whitespace text");
            RequireInvalidText(
                requestId,
                responseId,
                speakerId,
                new string('a', ConversationResponse.MaximumTextLength + 1),
                ConversationContractFailure.TextTooLong,
                "oversized text");
            RequireInvalidText(requestId, responseId, speakerId, "a\u0001b",
                ConversationContractFailure.InvalidText, "control character");
            RequireInvalidText(requestId, responseId, speakerId, "a\uD800b",
                ConversationContractFailure.InvalidText, "unpaired surrogate");
            Require(
                !ConversationResponse.TryCreate(
                    requestId,
                    responseId,
                    speakerId,
                    "Hello",
                    (ConversationPresentationIntent)99,
                    out _,
                    out failure)
                && failure == ConversationContractFailure
                    .UnsupportedPresentationIntent,
                "Unsupported intent was accepted.");
        }

        private static void VerifyIdentifierSemantics()
        {
            Require(
                ConversationLogicalId.TryCreate("same.id", out var first)
                && ConversationLogicalId.TryCreate("same.id", out var second)
                && first == second
                && first.GetHashCode() == second.GetHashCode(),
                "Ordinal equality or hash was not stable.");
            Require(
                !ConversationLogicalId.TryCreate("Same.id", out _)
                && !ConversationLogicalId.TryCreate("same..id", out _)
                && !ConversationLogicalId.TryCreate(".same-id", out _)
                && !ConversationLogicalId.TryCreate("same-id.", out _),
                "Invalid logical ID form was accepted.");
            Require(
                !ConversationRequestId.TryCreate("request..003", out _),
                "Invalid request ID form was accepted.");

            var firstFailure = ConversationContractFailure.None;
            var secondFailure = ConversationContractFailure.None;
            ConversationResponse.TryCreate(
                default,
                default,
                default,
                null,
                ConversationPresentationIntent.CharacterUtterance,
                out _,
                out firstFailure);
            ConversationResponse.TryCreate(
                default,
                default,
                default,
                null,
                ConversationPresentationIntent.CharacterUtterance,
                out _,
                out secondFailure);
            Require(
                firstFailure == ConversationContractFailure.InvalidRequestId
                && secondFailure == firstFailure,
                "Repeated validation was not deterministic.");
        }

        private static void RequireInvalidText(
            ConversationRequestId requestId,
            ConversationLogicalId responseId,
            ConversationLogicalId speakerId,
            string text,
            ConversationContractFailure expectedFailure,
            string description)
        {
            Require(
                !ConversationResponse.TryCreate(
                    requestId,
                    responseId,
                    speakerId,
                    text,
                    ConversationPresentationIntent.CharacterUtterance,
                    out _,
                    out var failure)
                && failure == expectedFailure,
                "Invalid " + description + " did not report expected failure.");
        }

        private static void VerifyLogicalId(string value, string description)
        {
            Require(
                ConversationLogicalId.TryCreate(value, out _),
                "Built-in " + description + " trigger was invalid.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
