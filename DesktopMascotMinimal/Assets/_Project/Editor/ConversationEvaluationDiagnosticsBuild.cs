using System;
using System.IO;
using DesktopMascot.Diagnostics.Conversation;
using UnityEditor;
using UnityEngine;

namespace DesktopMascot.Editor
{
    public static class ConversationEvaluationDiagnosticsBuild
    {
        public static void Run()
        {
            var evaluationDirectory = Path.Combine(
                Application.dataPath,
                "_Project/Runtime/Conversation/Evaluation");
            var domainDirectory = Path.Combine(
                Application.dataPath,
                "_Project/Runtime/Conversation/Domain");
            if (!DesktopMascotConversationEvaluationDiagnostics.Run(out var failure))
            {
                throw new InvalidOperationException(
                    "[DesktopMascotConversationEvaluationDiagnostics] Failed: " +
                    failure);
            }

            VerifyEvaluationSourceDependencies(evaluationDirectory);
            VerifyDomainDoesNotReferenceEvaluation(domainDirectory);

            Debug.Log(
                "[DesktopMascotConversationEvaluationDiagnostics] Passed: " +
                "contracts, correlation, and dependency boundary.");
        }

        private static void VerifyEvaluationSourceDependencies(
            string evaluationDirectory)
        {
            if (!Directory.Exists(evaluationDirectory))
                throw new InvalidOperationException("Evaluation directory was unavailable.");

            var forbiddenTokens = new[]
            {
                "UnityEngine", "MonoBehaviour", "GameObject", "Camera",
                "RenderTexture", "SpeechMessage", "CharacterDescriptor",
                "System.IO", "System.Net", "System.Threading", "D3D12",
                "DirectComposition", "DXGI", "Win32", "Android", "HWND",
                "HRGN", "IConversationEvaluator", "Rule", "Script", "Pack",
                "Provider", "File", "Directory"
            };
            VerifyForbiddenTokens(
                evaluationDirectory,
                forbiddenTokens,
                "Forbidden evaluation dependency token: ");
        }

        private static void VerifyDomainDoesNotReferenceEvaluation(
            string domainDirectory)
        {
            if (!Directory.Exists(domainDirectory))
                throw new InvalidOperationException("Domain directory was unavailable.");

            VerifyForbiddenTokens(
                domainDirectory,
                new[] { "Conversation.Evaluation", "EvaluationResult" },
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
