using System;
using System.IO;
using DesktopMascot.Diagnostics.Conversation;
using UnityEditor;
using UnityEngine;

namespace DesktopMascot.Editor
{
    public static class LocalConversationResponseEntryDiagnosticsBuild
    {
        public static void Run()
        {
            var evaluationDirectory = Path.Combine(
                Application.dataPath,
                "_Project/Runtime/Conversation/Evaluation");
            var domainDirectory = Path.Combine(
                Application.dataPath,
                "_Project/Runtime/Conversation/Domain");
            if (!DesktopMascotLocalConversationResponseEntryDiagnostics.Run(
                    out var failure))
            {
                throw new InvalidOperationException(
                    "[DesktopMascotLocalConversationResponseEntryDiagnostics] Failed: " +
                    failure);
            }

            VerifyEvaluationSourceDependencies(evaluationDirectory);
            VerifyEntryIsRequestIndependent(evaluationDirectory);
            VerifyDomainDoesNotReferenceEvaluation(domainDirectory);

            Debug.Log(
                "[DesktopMascotLocalConversationResponseEntryDiagnostics] Passed: " +
                "entry contract and dependency boundary.");
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

        private static void VerifyEntryIsRequestIndependent(
            string evaluationDirectory)
        {
            var entryPath = Path.Combine(
                evaluationDirectory,
                "LocalConversationResponseEntry.cs");
            if (!File.Exists(entryPath))
                throw new InvalidOperationException("Entry source was unavailable.");

            VerifyForbiddenTokens(
                entryPath,
                new[] { "RequestId", "CharacterId", "CharacterDescriptor" },
                "Forbidden entry contract token: ");
        }

        private static void VerifyDomainDoesNotReferenceEvaluation(
            string domainDirectory)
        {
            VerifyForbiddenTokens(
                domainDirectory,
                new[]
                {
                    "Conversation.Evaluation", "EvaluationResult",
                    "ILocalConversationEvaluator", "LocalConversationResponseEntry"
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
                    throw new InvalidOperationException(errorPrefix + forbiddenToken);
            }
        }
    }
}
