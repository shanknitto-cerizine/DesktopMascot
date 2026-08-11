using System;
using System.IO;
using DesktopMascot.Diagnostics.Conversation;
using UnityEditor;
using UnityEngine;

namespace DesktopMascot.Editor
{
    public static class ConversationDomainDiagnosticsBuild
    {
        public static void Run()
        {
            var domainDirectory = Path.Combine(
                Application.dataPath,
                "_Project/Runtime/Conversation/Domain");
            if (!DesktopMascotConversationDomainDiagnostics.Run(out var failure))
            {
                throw new InvalidOperationException(
                    "[DesktopMascotConversationDomainDiagnostics] Failed: " +
                    failure);
            }

            VerifyDomainSourceDependencies(domainDirectory);

            Debug.Log(
                "[DesktopMascotConversationDomainDiagnostics] Passed: " +
                "contracts, validation, IDs, and dependency boundary.");
        }

        private static void VerifyDomainSourceDependencies(string domainDirectory)
        {
            if (!Directory.Exists(domainDirectory))
                throw new InvalidOperationException("Domain directory was unavailable.");

            var forbiddenTokens = new[]
            {
                "UnityEngine", "MonoBehaviour", "GameObject", "Camera",
                "RenderTexture", "SpeechMessage", "CharacterDescriptor",
                "System.IO", "System.Net", "System.Threading", "D3D12",
                "DirectComposition", "DXGI", "Win32", "Android", "HWND",
                "HRGN"
            };
            foreach (var file in Directory.GetFiles(domainDirectory, "*.cs"))
            {
                var source = File.ReadAllText(file);
                foreach (var forbiddenToken in forbiddenTokens)
                {
                    if (source.IndexOf(
                            forbiddenToken,
                            StringComparison.Ordinal) >= 0)
                    {
                        throw new InvalidOperationException(
                            "Forbidden domain dependency token: " +
                            forbiddenToken);
                    }
                }
            }
        }
    }
}
