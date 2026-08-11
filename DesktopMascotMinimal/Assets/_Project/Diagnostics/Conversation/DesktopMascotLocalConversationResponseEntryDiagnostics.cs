using System;
using DesktopMascot.Runtime.Conversation.Domain;
using DesktopMascot.Runtime.Conversation.Evaluation;

namespace DesktopMascot.Diagnostics.Conversation
{
    public static class DesktopMascotLocalConversationResponseEntryDiagnostics
    {
        public static bool Run(out string failure)
        {
            failure = null;
            try
            {
                VerifyValidConstructionAndValuePreservation();
                VerifyIdentifierFailures();
                VerifyTextValidation();
                VerifyPresentationIntentValidation();
                VerifyNoRepairAndDeterminism();
                return true;
            }
            catch (InvalidOperationException exception)
            {
                failure = exception.Message;
                return false;
            }
        }

        private static void VerifyValidConstructionAndValuePreservation()
        {
            var triggerId = CreateId("desktop-mascot.trigger.mascot-click");
            var responseId = CreateId("response.diagnostic");
            var speakerId = CreateId("speaker.diagnostic");
            Require(LocalConversationResponseEntry.TryCreate(
                    triggerId,
                    responseId,
                    speakerId,
                    "Diagnostic\r\nentry\ttext.",
                    ConversationPresentationIntent.CharacterUtterance,
                    out var entry,
                    out var failure)
                && failure == ConversationContractFailure.None
                && entry.TriggerId == triggerId
                && entry.ResponseId == responseId
                && entry.SpeakerId == speakerId
                && entry.Text == "Diagnostic\r\nentry\ttext."
                && entry.PresentationIntent
                    == ConversationPresentationIntent.CharacterUtterance,
                "Valid entry was rejected or changed its values.");
        }

        private static void VerifyIdentifierFailures()
        {
            var triggerId = CreateId("trigger.diagnostic");
            var responseId = CreateId("response.diagnostic");
            var speakerId = CreateId("speaker.diagnostic");
            RequireFailure(default, responseId, speakerId, "Text",
                ConversationPresentationIntent.CharacterUtterance,
                ConversationContractFailure.InvalidTriggerId,
                "default trigger ID");
            RequireFailure(triggerId, default, speakerId, "Text",
                ConversationPresentationIntent.CharacterUtterance,
                ConversationContractFailure.InvalidResponseId,
                "default response ID");
            RequireFailure(triggerId, responseId, default, "Text",
                ConversationPresentationIntent.CharacterUtterance,
                ConversationContractFailure.InvalidSpeakerId,
                "default speaker ID");
        }

        private static void VerifyTextValidation()
        {
            var triggerId = CreateId("trigger.text");
            var responseId = CreateId("response.text");
            var speakerId = CreateId("speaker.text");
            RequireFailure(triggerId, responseId, speakerId, null,
                ConversationPresentationIntent.CharacterUtterance,
                ConversationContractFailure.MissingRequiredValue, "null text");
            RequireFailure(triggerId, responseId, speakerId, " \t\r\n",
                ConversationPresentationIntent.CharacterUtterance,
                ConversationContractFailure.EmptyText, "whitespace text");
            Require(LocalConversationResponseEntry.TryCreate(
                    triggerId, responseId, speakerId,
                    new string('a', ConversationResponse.MaximumTextLength),
                    ConversationPresentationIntent.CharacterUtterance,
                    out _, out var acceptedFailure)
                && acceptedFailure == ConversationContractFailure.None,
                "Maximum-length text was rejected.");
            RequireFailure(triggerId, responseId, speakerId,
                new string('a', ConversationResponse.MaximumTextLength + 1),
                ConversationPresentationIntent.CharacterUtterance,
                ConversationContractFailure.TextTooLong, "oversized text");
            RequireFailure(triggerId, responseId, speakerId, "a\u0001b",
                ConversationPresentationIntent.CharacterUtterance,
                ConversationContractFailure.InvalidText, "control character");
            RequireFailure(triggerId, responseId, speakerId, "a\uD800b",
                ConversationPresentationIntent.CharacterUtterance,
                ConversationContractFailure.InvalidText, "unpaired high surrogate");
            RequireFailure(triggerId, responseId, speakerId, "a\uDC00b",
                ConversationPresentationIntent.CharacterUtterance,
                ConversationContractFailure.InvalidText, "unpaired low surrogate");
        }

        private static void VerifyPresentationIntentValidation()
        {
            RequireFailure(CreateId("trigger.intent"), CreateId("response.intent"),
                CreateId("speaker.intent"), "Text", (ConversationPresentationIntent)99,
                ConversationContractFailure.UnsupportedPresentationIntent,
                "unsupported presentation intent");
        }

        private static void VerifyNoRepairAndDeterminism()
        {
            Require(ConversationLogicalId.TryCreate("same.entry", out var firstId)
                && ConversationLogicalId.TryCreate("same.entry", out var secondId)
                && firstId == secondId
                && firstId.GetHashCode() == secondId.GetHashCode(),
                "Ordinal logical-ID semantics were not preserved.");
            var triggerId = CreateId("trigger.repeat");
            var responseId = CreateId("response.repeat");
            var speakerId = CreateId("speaker.repeat");
            const string text = "  No trim or normalization.  ";
            Require(LocalConversationResponseEntry.TryCreate(
                    triggerId, responseId, speakerId, text,
                    ConversationPresentationIntent.CharacterUtterance,
                    out var first, out var firstFailure)
                && LocalConversationResponseEntry.TryCreate(
                    triggerId, responseId, speakerId, text,
                    ConversationPresentationIntent.CharacterUtterance,
                    out var second, out var secondFailure)
                && firstFailure == ConversationContractFailure.None
                && secondFailure == firstFailure
                && first.TriggerId == second.TriggerId
                && first.ResponseId == second.ResponseId
                && first.SpeakerId == second.SpeakerId
                && first.Text == text
                && second.Text == text
                && first.PresentationIntent == second.PresentationIntent,
                "Entry construction was not deterministic or repaired text.");
        }

        private static ConversationLogicalId CreateId(string value)
        {
            Require(ConversationLogicalId.TryCreate(value, out var logicalId),
                "Valid logical ID was rejected.");
            return logicalId;
        }

        private static void RequireFailure(
            ConversationLogicalId triggerId,
            ConversationLogicalId responseId,
            ConversationLogicalId speakerId,
            string text,
            ConversationPresentationIntent presentationIntent,
            ConversationContractFailure expectedFailure,
            string description)
        {
            Require(!LocalConversationResponseEntry.TryCreate(
                    triggerId, responseId, speakerId, text, presentationIntent,
                    out var entry, out var failure)
                && entry == null
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
