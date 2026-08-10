using DesktopMascot.Character;
using DesktopMascot.Runtime.Platform.Windows.Speech;
using UnityEngine;

namespace DesktopMascot.Runtime.Presentation.Speech
{
    internal enum SpeechPresentationState { Hidden, Visible }
    internal enum SpeechShowResult { Accepted, Coalesced, Busy, Invalid, Shutdown, Failed }
    internal enum SpeechCloseSource { Click, Timeout, Explicit }

    internal sealed class SpeechPresentationController : MonoBehaviour
    {
        private const float TimeoutSeconds = 12.0f;
        private readonly SpeechAnchorResolver anchorResolver = new();
        private ISpeechPresentationHost host;
        private SpeechPresentationView view;
        private Camera anchorCamera;
        private SpeechMessage currentMessage;
        private SpeechPresentationState state;
        private bool shutdownStarted;
        private uint presentationGeneration;
        private uint closeClaimedGeneration;
        private int failureStage;
        private float timeoutAt;
        private uint showPresentBaseline;
        private Vector2Int lastAnchor = new(128, 128);

        internal int ShowRequestCount { get; private set; }
        internal int ShowAcceptedCount { get; private set; }
        internal int ShowCoalescedCount { get; private set; }
        internal int ShowBusyCount { get; private set; }
        internal int ShowFailedCount { get; private set; }
        internal int CloseClickCount { get; private set; }
        internal int CloseTimeoutCount { get; private set; }
        internal int CloseExplicitCount { get; private set; }
        internal int StaleCallbackCount { get; private set; }
        internal int FirstCloseWinsCount { get; private set; }
        internal int ShutdownSuppressionCount { get; private set; }
        internal SpeechPresentationState State => state;
        internal uint PresentationGeneration => presentationGeneration;
        internal SpeechMessage CurrentMessage => currentMessage;
        internal int FailureStage => failureStage != 0 ? failureStage : host?.FailureStage ?? 0;
        internal SpeechAnchorResolver AnchorResolver => anchorResolver;
        internal SpeechPresentationView View => view;
        internal ISpeechPresentationHost Host => host;

        internal bool Initialize(CharacterAssetManager manager, Camera camera)
        {
            if (host != null || view != null)
                return false;
            anchorCamera = camera;
            anchorResolver.Configure(manager);
            view = new SpeechPresentationView();
            if (!view.Initialize())
            {
                failureStage = 1;
                return false;
            }
            host = new WindowsSpeechPresentationHost();
            if (!host.Initialize())
            {
                failureStage = 2;
                view.Cleanup();
                return false;
            }
            return true;
        }

        internal SpeechShowResult TryShow(SpeechMessage message)
        {
            ++ShowRequestCount;
            if (shutdownStarted) { ++ShutdownSuppressionCount; return SpeechShowResult.Shutdown; }
            if (message == null || !message.IsValid) return SpeechShowResult.Invalid;
            if (state == SpeechPresentationState.Visible)
            {
                if (currentMessage.MessageId == message.MessageId)
                { ++ShowCoalescedCount; return SpeechShowResult.Coalesced; }
                ++ShowBusyCount; return SpeechShowResult.Busy;
            }
            if (host == null || view == null || !host.IsReady || !view.Apply(message))
            { ++ShowFailedCount; failureStage = failureStage == 0 ? 3 : failureStage; return SpeechShowResult.Failed; }
            if (!anchorResolver.TryResolve(anchorCamera, out lastAnchor))
                lastAnchor = new Vector2Int(128, 128);
            var generation = presentationGeneration + 1;
            if (!host.Show(generation, lastAnchor))
            { view.Hide(); ++ShowFailedCount; failureStage = 4; return SpeechShowResult.Failed; }
            presentationGeneration = generation;
            closeClaimedGeneration = 0;
            currentMessage = message;
            state = SpeechPresentationState.Visible;
            timeoutAt = Time.realtimeSinceStartup + TimeoutSeconds;
            showPresentBaseline = host.PresentCount;
            ++ShowAcceptedCount;
            return SpeechShowResult.Accepted;
        }

        internal bool TryClose(uint generation, SpeechCloseSource source)
        {
            if (generation == presentationGeneration
                && closeClaimedGeneration == generation)
            { ++FirstCloseWinsCount; return false; }
            if (shutdownStarted || state != SpeechPresentationState.Visible
                || generation != presentationGeneration)
            { ++StaleCallbackCount; return false; }
            closeClaimedGeneration = generation;
            if (!host.Hide(generation))
            { closeClaimedGeneration = 0; failureStage = 5; return false; }
            switch (source)
            {
                case SpeechCloseSource.Click: ++CloseClickCount; break;
                case SpeechCloseSource.Timeout: ++CloseTimeoutCount; break;
                default: ++CloseExplicitCount; break;
            }
            currentMessage = null;
            state = SpeechPresentationState.Hidden;
            view.Hide();
            return true;
        }

        internal bool BeginShutdown()
        {
            if (shutdownStarted) return true;
            shutdownStarted = true;
            currentMessage = null;
            state = SpeechPresentationState.Hidden;
            return host == null || host.BeginShutdown();
        }

        internal bool Cleanup()
        {
            BeginShutdown();
            var hostClean = host == null || host.Cleanup();
            var viewClean = view == null || view.Cleanup();
            Debug.Log(
                "[DesktopMascotSpeech] Host/View cleanup result: " +
                $"{hostClean}/{viewClean}");
            host = null; view = null; anchorCamera = null;
            return hostClean && viewClean;
        }

        internal bool SetCurrentTimeoutForDiagnostics(float seconds)
        {
            if (shutdownStarted
                || state != SpeechPresentationState.Visible
                || seconds <= 0.0f)
                return false;
            timeoutAt = Time.realtimeSinceStartup + seconds;
            return true;
        }

        private void LateUpdate()
        {
            if (shutdownStarted || host == null)
                return;
            host.Poll();
            if (state != SpeechPresentationState.Visible)
                return;
            if (anchorResolver.TryResolve(anchorCamera, out var anchor)
                && anchor != lastAnchor)
            { lastAnchor = anchor; host.UpdateAnchor(anchor); }
            var clickGeneration = host.ConsumeClickGeneration();
            if (clickGeneration != 0)
                TryClose(clickGeneration, SpeechCloseSource.Click);
            if (state == SpeechPresentationState.Visible
                && Time.realtimeSinceStartup >= timeoutAt)
                TryClose(presentationGeneration, SpeechCloseSource.Timeout);
            if (state == SpeechPresentationState.Visible
                && host.PresentCount == showPresentBaseline)
            {
                host.IssueRenderEvent(view.RenderTexture.GetNativeTexturePtr());
            }
        }
    }
}
