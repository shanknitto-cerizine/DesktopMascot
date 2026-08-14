using System;
using System.Collections.Generic;
using DesktopMascot.Runtime.Conversation;
using DesktopMascot.Runtime.Conversation.Domain;
using DesktopMascot.Runtime.Conversation.Evaluation;

namespace DesktopMascot.Diagnostics.Conversation
{
    public static class DesktopMascotMascotClickConversationDiagnostics
    {
        public static bool Run(out string failure)
        {
            failure = null;
            try
            {
                VerifyInitialCursorAndRepeatedSnapshot();
                VerifyOneAndMultipleClickDeltas();
                VerifyDebtDrainsWithoutLoss();
                VerifyModuloGenerationHandling();
                VerifyIdsAndCorrelatedSingleEvaluation();
                VerifyRequestIdExhaustionDoesNotWrap();
                VerifyShutdownAndIdempotentCleanup();
                return true;
            }
            catch (InvalidOperationException exception)
            {
                failure = exception.Message;
                return false;
            }
        }

        private static void VerifyInitialCursorAndRepeatedSnapshot()
        {
            var source = new SnapshotSource { Value = 41 };
            var evaluator = new RecordingEvaluator();
            var controller = Create(source, evaluator);
            controller.Initialize();
            controller.Pump();
            Require(evaluator.Requests.Count == 0,
                "Initial cursor replayed completed-click history.");
            source.Value = 42;
            controller.Pump();
            controller.Pump();
            Require(evaluator.Requests.Count == 1,
                "Repeated snapshot produced a duplicate evaluation.");
        }

        private static void VerifyOneAndMultipleClickDeltas()
        {
            var source = new SnapshotSource { Value = 7 };
            var evaluator = new RecordingEvaluator();
            var controller = Create(source, evaluator);
            controller.Initialize();
            source.Value = 8;
            controller.Pump();
            source.Value = 13;
            controller.Pump();
            Require(evaluator.Requests.Count == 6,
                "One or multiple click delta did not produce one evaluation per click.");
        }

        private static void VerifyDebtDrainsWithoutLoss()
        {
            var source = new SnapshotSource { Value = 0 };
            var evaluator = new RecordingEvaluator();
            var controller = Create(source, evaluator);
            controller.Initialize();
            source.Value = 140;
            controller.Pump();
            Require(evaluator.Requests.Count == 64
                && controller.PendingClickCount == 76,
                "Initial bounded debt drain was incorrect.");
            controller.Pump();
            Require(evaluator.Requests.Count == 128
                && controller.PendingClickCount == 12,
                "Second bounded debt drain was incorrect.");
            controller.Pump();
            Require(evaluator.Requests.Count == 140
                && controller.PendingClickCount == 0,
                "Debt did not drain across frames without loss.");
        }

        private static void VerifyModuloGenerationHandling()
        {
            var source = new SnapshotSource { Value = ulong.MaxValue - 1 };
            var evaluator = new RecordingEvaluator();
            var controller = Create(source, evaluator);
            controller.Initialize();
            source.Value = 1;
            controller.Pump();
            Require(evaluator.Requests.Count == 3,
                "Unsigned modulo completed-click generation handling failed.");
        }

        private static void VerifyIdsAndCorrelatedSingleEvaluation()
        {
            var source = new SnapshotSource { Value = 3 };
            var evaluator = new RecordingEvaluator();
            var controller = Create(source, evaluator);
            controller.Initialize();
            source.Value = 5;
            controller.Pump();
            Require(evaluator.Requests.Count == 2
                && evaluator.Requests[0].RequestId.Value == "request-1"
                && evaluator.Requests[1].RequestId.Value == "request-2"
                && evaluator.Requests[0].Event.TriggerId.Value
                    == ConversationTriggerIds.MascotClick
                && evaluator.Requests[0].CharacterId.Value
                    == "desktop-mascot.character.default"
                && evaluator.Results[0].RequestId == evaluator.Requests[0].RequestId
                && evaluator.Results[1].RequestId == evaluator.Requests[1].RequestId,
                "Request identity, fixed IDs, or result correlation was incorrect.");
        }

        private static void VerifyRequestIdExhaustionDoesNotWrap()
        {
            var source = new SnapshotSource { Value = 0 };
            var evaluator = new RecordingEvaluator();
            var controller = new MascotClickConversationController(
                () => source.Value,
                evaluator,
                ulong.MaxValue);
            controller.Initialize();
            source.Value = 2;
            controller.Pump();
            Require(evaluator.Requests.Count == 1
                && evaluator.Requests[0].RequestId.Value
                    == "request-18446744073709551615"
                && controller.RequestIdExhausted
                && controller.PendingClickCount == 1,
                "Request ID exhaustion wrapped or discarded pending work.");
            controller.Pump();
            Require(evaluator.Requests.Count == 1,
                "Request ID exhaustion allocated a wrapped request ID.");
        }

        private static void VerifyShutdownAndIdempotentCleanup()
        {
            var source = new SnapshotSource { Value = 0 };
            var evaluator = new RecordingEvaluator();
            var controller = Create(source, evaluator);
            controller.Initialize();
            controller.BeginShutdown();
            source.Value = 1;
            controller.Pump();
            Require(evaluator.Requests.Count == 0 && !controller.Accepting,
                "Shutdown accepted a later completed click.");
            Require(controller.Cleanup() && controller.Cleanup()
                && controller.CleanupCompleted,
                "Mascot click conversation cleanup was not idempotent.");
        }

        private static MascotClickConversationController Create(
            SnapshotSource source,
            RecordingEvaluator evaluator)
        {
            return new MascotClickConversationController(
                () => source.Value, evaluator);
        }

        private sealed class SnapshotSource
        {
            internal ulong Value;
        }

        private sealed class RecordingEvaluator : ILocalConversationEvaluator
        {
            internal readonly List<ConversationRequest> Requests =
                new List<ConversationRequest>();
            internal readonly List<ConversationEvaluationResult> Results =
                new List<ConversationEvaluationResult>();

            public ConversationEvaluationResult Evaluate(ConversationRequest request)
            {
                Requests.Add(request);
                Require(ConversationEvaluationResult.TryCreate(
                        ConversationEvaluationOutcome.NoMatch,
                        request.RequestId,
                        null,
                        out var result,
                        out _),
                    "Diagnostic evaluator could not create a correlated result.");
                Results.Add(result);
                return result;
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
