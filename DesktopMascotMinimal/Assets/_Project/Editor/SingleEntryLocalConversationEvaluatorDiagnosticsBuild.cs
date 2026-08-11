using System;
using System.IO;
using DesktopMascot.Diagnostics.Conversation;
using UnityEditor;
using UnityEngine;

namespace DesktopMascot.Editor
{
    public static class SingleEntryLocalConversationEvaluatorDiagnosticsBuild
    {
        public static void Run()
        {
            var evaluationDirectory = Path.Combine(
                Application.dataPath,
                "_Project/Runtime/Conversation/Evaluation");
            var domainDirectory = Path.Combine(
                Application.dataPath,
                "_Project/Runtime/Conversation/Domain");
            if (!DesktopMascotSingleEntryLocalConversationEvaluatorDiagnostics.Run(
                    out var failure))
            {
                throw new InvalidOperationException(
                    "[DesktopMascotSingleEntryLocalConversationEvaluatorDiagnostics] " +
                    "Failed: " + failure);
            }

            VerifyEvaluationSourceDependencies(evaluationDirectory);
            VerifySingleEntryEvaluatorContract(evaluationDirectory);
            VerifyDomainDoesNotReferenceEvaluation(domainDirectory);

            Debug.Log(
                "[DesktopMascotSingleEntryLocalConversationEvaluatorDiagnostics] " +
                "Passed: single-entry exact-match evaluator and dependency boundary.");
        }

        private static void VerifyEvaluationSourceDependencies(
            string evaluationDirectory)
        {
            VerifyForbiddenTokens(
                evaluationDirectory,
                new[]
                {
                    "UnityEngine", "MonoBehaviour", "GameObject", "Camera",
                    "RenderTexture", "SpeechMessage", "CharacterDescriptor",
                    "System.IO", "System.Net", "System.Threading", "D3D12",
                    "DirectComposition", "DXGI", "Win32", "Android", "HWND",
                    "HRGN", "Task", "ValueTask", "CancellationToken", "Timer",
                    "Thread", "Queue", "Cache", "Rule", "Script", "Pack",
                    "Provider", "File", "Directory"
                },
                "Forbidden evaluation dependency token: ");
        }

        private static void VerifySingleEntryEvaluatorContract(
            string evaluationDirectory)
        {
            var evaluatorPath = Path.Combine(
                evaluationDirectory,
                "SingleEntryLocalConversationEvaluator.cs");
            if (!File.Exists(evaluatorPath))
            {
                throw new InvalidOperationException(
                    "Single-entry evaluator source was unavailable.");
            }

            VerifyForbiddenTokens(
                evaluatorPath,
                new[]
                {
                    "CharacterId", "CharacterDescriptor", "[", "List<",
                    "Dictionary", "IEnumerable", "foreach", "Registry",
                    "Priority", "Weight", "Random", "Fallback"
                },
                "Forbidden single-entry evaluator token: ");
        }

        private static void VerifyDomainDoesNotReferenceEvaluation(
            string domainDirectory)
        {
            VerifyForbiddenTokens(
                domainDirectory,
                new[]
                {
                    "Conversation.Evaluation", "EvaluationResult",
                    "ILocalConversationEvaluator", "LocalConversationResponseEntry",
                    "SingleEntryLocalConversationEvaluator"
                },
                "Forbidden Domain-to-Evaluation dependency token: ");
        }

        private static void VerifyForbiddenTokens(
            string path,
            string[] forbiddenTokens,
            string errorPrefix)
        {
            if (Directory.Exists(path))
            {
                foreach (var file in Directory.GetFiles(path, "*.cs"))
                    VerifyForbiddenTokens(file, forbiddenTokens, errorPrefix);
                return;
            }

            var source = File.ReadAllText(path);
            foreach (var forbiddenToken in forbiddenTokens)
            {
                if (source.IndexOf(forbiddenToken, StringComparison.Ordinal) >= 0)
                {
                    throw new InvalidOperationException(
                        errorPrefix + forbiddenToken);
                }
            }
        }
    }
}
