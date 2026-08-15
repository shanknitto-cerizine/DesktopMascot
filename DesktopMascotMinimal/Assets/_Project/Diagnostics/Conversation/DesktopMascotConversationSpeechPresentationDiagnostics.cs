using System;
using DesktopMascot.Runtime.Conversation;
using DesktopMascot.Runtime.Conversation.Domain;
using DesktopMascot.Runtime.Conversation.Evaluation;
using DesktopMascot.Runtime.Presentation.Speech;

namespace DesktopMascot.Diagnostics.Conversation
{
    public static class DesktopMascotConversationSpeechPresentationDiagnostics
    {
        public static bool Run(out string failure)
        {
            failure = null;
            try
            {
                VerifyResponseConversionAndAdmission();
                VerifyNoMatchAndShutdown();
                VerifySpeechFailureAndInvariantBoundary();
                return true;
            }
            catch (InvalidOperationException exception)
            {
                failure = exception.Message;
                return false;
            }
        }

        private static void VerifyResponseConversionAndAdmission()
        {
            SpeechMessage captured = null;
            var next = SpeechShowResult.Accepted;
            var adapter = new ConversationSpeechPresentationAdapter(message =>
            {
                captured = message;
                return next;
            });
            var result = CreateResponseProduced("request-1", "body");
            Require(adapter.Present(result)
                    == ConversationSpeechPresentationResult.Accepted
                && captured != null
                && captured.MessageId == "request-1"
                && captured.Speaker.SpeakerId
                    == "desktop-mascot.character.default"
                && captured.Speaker.DisplayName == "Mascot"
                && captured.Body == "body"
                && captured.Theme == SpeechThemeId.Normal
                && captured.ClosePolicy == SpeechClosePolicy.ManualOrTimeout
                && captured.Transition == SpeechTransitionStyle.None
                && captured.Speaker.Decoration == SpeechDecorationKind.None,
                "Response conversion did not preserve the M-057 contract.");

            next = SpeechShowResult.Coalesced;
            Require(adapter.Present(CreateResponseProduced("request-2", "body"))
                    == ConversationSpeechPresentationResult.Coalesced,
                "Coalesced admission was not preserved.");
            next = SpeechShowResult.Busy;
            Require(adapter.Present(CreateResponseProduced("request-3", "body"))
                    == ConversationSpeechPresentationResult.BusyDropped
                && adapter.BusyDroppedCount == 1,
                "Busy did not drop without a backlog.");
        }

        private static void VerifyNoMatchAndShutdown()
        {
            var invoked = 0;
            var adapter = new ConversationSpeechPresentationAdapter(_ =>
            {
                ++invoked;
                return SpeechShowResult.Accepted;
            });
            Require(adapter.Present(CreateNoMatch("request-4"))
                    == ConversationSpeechPresentationResult.NoPresentation
                && invoked == 0
                && adapter.NoMatchCount == 1,
                "NoMatch attempted a presentation.");
            adapter.BeginShutdown();
            Require(adapter.Present(CreateResponseProduced("request-5", "body"))
                    == ConversationSpeechPresentationResult.ShutdownRejected
                && invoked == 0
                && adapter.Cleanup()
                && adapter.Cleanup(),
                "Shutdown acceptance or cleanup was not idempotent.");
        }

        private static void VerifySpeechFailureAndInvariantBoundary()
        {
            var adapter = new ConversationSpeechPresentationAdapter(
                _ => SpeechShowResult.Failed);
            Require(adapter.Present(CreateResponseProduced("request-6", "body"))
                    == ConversationSpeechPresentationResult.SpeechFailed
                && adapter.SpeechFailedCount == 1,
                "Speech failure was not isolated as a dropped response.");
            var threw = false;
            try
            {
                adapter.Present(null);
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }
            Require(threw && adapter.InvariantFailedCount == 1,
                "Null result did not remain an invariant failure.");
        }

        private static ConversationEvaluationResult CreateNoMatch(string request)
        {
            ConversationEvaluationResult result = null;
            Require(ConversationRequestId.TryCreate(request, out var requestId)
                && ConversationEvaluationResult.TryCreate(
                    ConversationEvaluationOutcome.NoMatch,
                    requestId,
                    null,
                    out result,
                    out _),
                "Could not create a NoMatch diagnostic result.");
            return result;
        }

        private static ConversationEvaluationResult CreateResponseProduced(
            string request, string body)
        {
            ConversationEvaluationResult result = null;
            Require(ConversationRequestId.TryCreate(request, out var requestId)
                && ConversationLogicalId.TryCreate(
                    "desktop-mascot.response.mascot-click.default", out var responseId)
                && ConversationLogicalId.TryCreate(
                    "desktop-mascot.character.default", out var speakerId)
                && ConversationResponse.TryCreate(
                    requestId,
                    responseId,
                    speakerId,
                    body,
                    ConversationPresentationIntent.CharacterUtterance,
                    out var response,
                    out _)
                && ConversationEvaluationResult.TryCreate(
                    ConversationEvaluationOutcome.ResponseProduced,
                    requestId,
                    response,
                    out result,
                    out _),
                "Could not create a response-produced diagnostic result.");
            return result;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
