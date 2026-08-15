using System;
using DesktopMascot.Runtime.Conversation.Domain;
using DesktopMascot.Runtime.Conversation.Evaluation;
using DesktopMascot.Runtime.Presentation.Speech;

namespace DesktopMascot.Runtime.Conversation
{
    internal enum ConversationSpeechPresentationResult
    {
        NoPresentation,
        Accepted,
        Coalesced,
        BusyDropped,
        ShutdownRejected,
        SpeechFailed
    }

    // Converts one evaluated domain response into an independently owned Speech request.
    internal sealed class ConversationSpeechPresentationAdapter
    {
        private const string SpeakerDisplayName = "Mascot";
        private readonly Func<SpeechMessage, SpeechShowResult> tryShow;
        private bool accepting = true;
        private bool cleanupCompleted;

        internal ConversationSpeechPresentationAdapter(
            Func<SpeechMessage, SpeechShowResult> tryShow)
        {
            this.tryShow = tryShow
                ?? throw new ArgumentNullException(nameof(tryShow));
        }

        internal bool Accepting => accepting;
        internal bool CleanupCompleted => cleanupCompleted;
        internal int ReceivedCount { get; private set; }
        internal int ResponseProducedCount { get; private set; }
        internal int NoMatchCount { get; private set; }
        internal int AcceptedCount { get; private set; }
        internal int CoalescedCount { get; private set; }
        internal int BusyDroppedCount { get; private set; }
        internal int ShutdownRejectedCount { get; private set; }
        internal int SpeechFailedCount { get; private set; }
        internal int InvariantFailedCount { get; private set; }

        internal ConversationSpeechPresentationResult Present(
            ConversationEvaluationResult result)
        {
            ++ReceivedCount;
            if (!accepting)
            {
                ++ShutdownRejectedCount;
                return ConversationSpeechPresentationResult.ShutdownRejected;
            }

            if (result == null)
                throw Invariant("Conversation result was null.");

            switch (result.Outcome)
            {
                case ConversationEvaluationOutcome.NoMatch:
                    if (result.Response != null)
                        throw Invariant("NoMatch result contained a response.");
                    ++NoMatchCount;
                    return ConversationSpeechPresentationResult.NoPresentation;

                case ConversationEvaluationOutcome.ResponseProduced:
                    ++ResponseProducedCount;
                    return PresentResponseProduced(result);

                default:
                    throw Invariant("Conversation result outcome was unsupported.");
            }
        }

        internal void BeginShutdown() => accepting = false;

        internal bool Cleanup()
        {
            BeginShutdown();
            cleanupCompleted = true;
            return true;
        }

        private ConversationSpeechPresentationResult PresentResponseProduced(
            ConversationEvaluationResult result)
        {
            var response = result.Response;
            if (response == null
                || response.RequestId != result.RequestId
                || response.PresentationIntent
                    != ConversationPresentationIntent.CharacterUtterance)
            {
                throw Invariant("Conversation response violated presentation invariants.");
            }

            var message = new SpeechMessage(
                result.RequestId.Value,
                new SpeakerData(
                    response.SpeakerId.Value,
                    SpeakerDisplayName,
                    SpeechDecorationKind.None),
                response.Text,
                SpeechThemeId.Normal,
                SpeechClosePolicy.ManualOrTimeout,
                SpeechTransitionStyle.None);
            if (!message.IsValid)
                throw Invariant("Speech message conversion was invalid.");

            switch (tryShow(message))
            {
                case SpeechShowResult.Accepted:
                    ++AcceptedCount;
                    return ConversationSpeechPresentationResult.Accepted;
                case SpeechShowResult.Coalesced:
                    ++CoalescedCount;
                    return ConversationSpeechPresentationResult.Coalesced;
                case SpeechShowResult.Busy:
                    ++BusyDroppedCount;
                    return ConversationSpeechPresentationResult.BusyDropped;
                case SpeechShowResult.Shutdown:
                    ++ShutdownRejectedCount;
                    return ConversationSpeechPresentationResult.ShutdownRejected;
                case SpeechShowResult.Failed:
                    ++SpeechFailedCount;
                    return ConversationSpeechPresentationResult.SpeechFailed;
                case SpeechShowResult.Invalid:
                    throw Invariant("Speech controller rejected a converted message.");
                default:
                    throw Invariant("Speech controller returned an unsupported result.");
            }
        }

        private InvalidOperationException Invariant(string message)
        {
            ++InvariantFailedCount;
            return new InvalidOperationException(message);
        }
    }
}
