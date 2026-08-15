using System;
using System.Globalization;
using DesktopMascot.Runtime.Conversation.Domain;
using DesktopMascot.Runtime.Conversation.Evaluation;

namespace DesktopMascot.Runtime.Conversation
{
    // Windows runtime owner for completed mascot-click request acceptance.
    internal sealed class MascotClickConversationController
    {
        private const ulong MaximumAcceptedClicksPerFrame = 64;
        private const string DefaultCharacterId =
            "desktop-mascot.character.default";
        private const string DefaultResponseId =
            "desktop-mascot.response.mascot-click.default";
        private const string DefaultText = "こんにちは。";

        private readonly Func<ulong> generationSnapshot;
        private readonly ILocalConversationEvaluator evaluator;
        private readonly ConversationSpeechPresentationAdapter presentationAdapter;
        private ulong observedGeneration;
        private ulong pendingClickCount;
        private ulong nextRequestNumber;
        private bool initialized;
        private bool accepting = true;
        private bool requestIdExhausted;
        private bool cleanupCompleted;

        internal MascotClickConversationController()
            : this(
                NativeMascotClickCompletionBridge.GetCompletedClickGeneration,
                CreateDefaultEvaluator(),
                1,
                null)
        {
        }

        internal MascotClickConversationController(
            ConversationSpeechPresentationAdapter presentationAdapter)
            : this(
                NativeMascotClickCompletionBridge.GetCompletedClickGeneration,
                CreateDefaultEvaluator(),
                1,
                presentationAdapter)
        {
        }

        internal MascotClickConversationController(
            Func<ulong> generationSnapshot,
            ILocalConversationEvaluator evaluator,
            ulong nextRequestNumber = 1,
            ConversationSpeechPresentationAdapter presentationAdapter = null)
        {
            this.generationSnapshot = generationSnapshot
                ?? throw new ArgumentNullException(nameof(generationSnapshot));
            this.evaluator = evaluator
                ?? throw new ArgumentNullException(nameof(evaluator));
            if (nextRequestNumber == 0)
                throw new ArgumentOutOfRangeException(nameof(nextRequestNumber));

            this.nextRequestNumber = nextRequestNumber;
            this.presentationAdapter = presentationAdapter;
        }

        internal bool Accepting => accepting;
        internal bool CleanupCompleted => cleanupCompleted;
        internal bool RequestIdExhausted => requestIdExhausted;
        internal ulong PendingClickCount => pendingClickCount;

        // The first native snapshot is a cursor, never a historical request.
        internal void Initialize()
        {
            if (initialized)
                return;

            observedGeneration = generationSnapshot();
            initialized = true;
        }

        internal void Pump()
        {
            if (!accepting || !initialized || requestIdExhausted)
                return;

            var currentGeneration = generationSnapshot();
            var delta = unchecked(currentGeneration - observedGeneration);
            observedGeneration = currentGeneration;
            if (ulong.MaxValue - pendingClickCount < delta)
            {
                accepting = false;
                throw new InvalidOperationException(
                    "Mascot click conversation pending debt overflowed.");
            }

            pendingClickCount += delta;
            for (var accepted = 0UL;
                 accepted < MaximumAcceptedClicksPerFrame
                 && pendingClickCount != 0
                 && !requestIdExhausted;
                 ++accepted)
            {
                var request = CreateRequest(AllocateRequestId());
                // Offer the correlated domain result to the optional presentation path.
                var result = evaluator.Evaluate(request);
                if (result == null || result.RequestId != request.RequestId)
                {
                    throw new InvalidOperationException(
                        "Mascot click conversation evaluator correlation failed.");
                }

                presentationAdapter?.Present(result);

                --pendingClickCount;
            }
        }

        internal void BeginShutdown()
        {
            accepting = false;
        }

        internal bool Cleanup()
        {
            BeginShutdown();
            cleanupCompleted = true;
            return true;
        }

        private ConversationRequestId AllocateRequestId()
        {
            var allocated = nextRequestNumber;
            if (allocated == ulong.MaxValue)
                requestIdExhausted = true;
            else
                ++nextRequestNumber;

            if (!ConversationRequestId.TryCreate(
                    "request-" + allocated.ToString(CultureInfo.InvariantCulture),
                    out var requestId))
            {
                throw new InvalidOperationException(
                    "Mascot click conversation request ID construction failed.");
            }

            return requestId;
        }

        private static ConversationRequest CreateRequest(
            ConversationRequestId requestId)
        {
            if (!ConversationLogicalId.TryCreate(
                    ConversationTriggerIds.MascotClick,
                    out var triggerId)
                || !ConversationLogicalId.TryCreate(
                    DefaultCharacterId,
                    out var characterId)
                || !ConversationEvent.TryCreate(
                    triggerId,
                    out var conversationEvent,
                    out _)
                || !ConversationRequest.TryCreate(
                    requestId,
                    conversationEvent,
                    characterId,
                    out var request,
                    out _))
            {
                throw new InvalidOperationException(
                    "Mascot click conversation request construction failed.");
            }

            return request;
        }

        private static ILocalConversationEvaluator CreateDefaultEvaluator()
        {
            if (!ConversationLogicalId.TryCreate(
                    ConversationTriggerIds.MascotClick,
                    out var triggerId)
                || !ConversationLogicalId.TryCreate(
                    DefaultResponseId,
                    out var responseId)
                || !ConversationLogicalId.TryCreate(
                    DefaultCharacterId,
                    out var speakerId)
                || !LocalConversationResponseEntry.TryCreate(
                    triggerId,
                    responseId,
                    speakerId,
                    DefaultText,
                    ConversationPresentationIntent.CharacterUtterance,
                    out var entry,
                    out _))
            {
                throw new InvalidOperationException(
                    "Mascot click conversation response entry construction failed.");
            }

            return new SingleEntryLocalConversationEvaluator(entry);
        }
    }
}
