using System;
using System.IO;
using DesktopMascot.Diagnostics.Conversation;
using UnityEditor;
using UnityEngine;

namespace DesktopMascot.Editor
{
    public static class LocalConversationEvaluatorDiagnosticsBuild
    {
        public static void Run()
        {
            var evaluationDirectory = Path.Combine(
                Application.dataPath,
                "_Project/Runtime/Conversation/Evaluation");
            var domainDirectory = Path.Combine(
                Application.dataPath,
                "_Project/Runtime/Conversation/Domain");
            if (!DesktopMascotLocalConversationEvaluatorDiagnostics.Run(out var failure))
            {
                throw new InvalidOperationException(
                    "[DesktopMascotLocalConversationEvaluatorDiagnostics] Failed: " +
                    failure);
            }

            VerifyEvaluatorSourceDependencies(evaluationDirectory);
            VerifyDomainDoesNotReferenceEvaluation(domainDirectory);

            Debug.Log(
                "[DesktopMascotLocalConversationEvaluatorDiagnostics] Passed: " +
                "local synchronous evaluator contract and dependency boundary.");
        }

        private static void VerifyEvaluatorSourceDependencies(
            string evaluationDirectory)
        {
            if (!Directory.Exists(evaluationDirectory))
                throw new InvalidOperationException("Evaluation directory was unavailable.");

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
                "Forbidden local evaluator dependency token: ");
        }

        private static void VerifyDomainDoesNotReferenceEvaluation(
            string domainDirectory)
        {
            if (!Directory.Exists(domainDirectory))
                throw new InvalidOperationException("Domain directory was unavailable.");

            VerifyForbiddenTokens(
                domainDirectory,
                new[] { "Conversation.Evaluation", "EvaluationResult", "ILocalConversationEvaluator" },
                "Forbidden Domain-to-Evaluation dependency token: ");
        }

        private static void VerifyForbiddenTokens(
            string directory,
            string[] forbiddenTokens,
            string errorPrefix)
        {
            foreach (var file in Directory.GetFiles(directory, "*.cs"))
            {
                var source = File.ReadAllText(file);
                foreach (var forbiddenToken in forbiddenTokens)
                {
                    if (source.IndexOf(
                            forbiddenToken,
                            StringComparison.Ordinal) >= 0)
                    {
                        throw new InvalidOperationException(
                            errorPrefix + forbiddenToken);
                    }
                }
            }
        }
    }
}
