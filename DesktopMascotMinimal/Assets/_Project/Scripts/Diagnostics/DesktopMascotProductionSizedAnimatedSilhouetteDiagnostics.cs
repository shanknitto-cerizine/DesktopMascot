using System;
using System.Collections;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace DesktopMascot.Diagnostics
{
    internal sealed class DesktopMascotProductionSizedAnimatedSilhouetteDiagnostics : MonoBehaviour
    {
        private const string Dll = "DesktopMascotNative";
        private const int Size = 256;
        private const int Threshold = 128;
        private const int PublishMs = 250;
        private const int PhaseMs = 2000;
        private const int TargetPresents = 1200;
        private const float VisualSeconds = 32;
        private const float TimeoutSeconds = 55;
        private readonly byte[] mask = new byte[Size * Size];
        private readonly uint[] finalGdi = new uint[5];
        private GCHandle pin;
        private RenderTexture source;
        private bool pending, orientationKnown, flipRows, shuttingDown;
        private int requestPhase, managedFailure, readbackErrors;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private int lastPhase = -1;
#endif
        private ulong generation;
        private float started, visualStarted, nextReadback;
        private bool previousBackground;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int DMN_GetCompositionInitializationState();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int DMN_IsDestinationTextureAvailable();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int DMN_WasReadbackValidationCompleted();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int DMN_DidReadbackExpectedOrientationMatch();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int DMN_StartAnimatedCompositionAlphaMaskDiagnostics(int w,int h,int s,int t);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int DMN_SubmitCompositionAlphaMask(IntPtr p,int w,int h,int s,ulong g);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern void DMN_StopCompositionAlphaMaskDiagnostics();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int DMN_SetContinuousCompositionTargetFrameCountForNextRun(uint n);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int DMN_StartProductionSizedAnimatedSilhouetteDiagnostics(int w,int h,int t,int fps,int presents,int publishMs,int phaseMs);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int DMN_PollProductionSizedAnimatedSilhouetteDiagnostics();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int DMN_CompleteProductionSizedAnimatedSilhouetteDiagnostics();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int DMN_StopProductionSizedAnimatedSilhouetteDiagnostics();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int DMN_GetProductionSizedAnimatedSilhouetteState();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int DMN_GetProductionSizedAnimatedSilhouetteFailureStage();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int DMN_GetProductionSizedAnimatedSilhouetteAppliedPhase();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern ulong DMN_GetProductionSizedAnimatedSilhouettePublishedGeneration();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern ulong DMN_GetProductionSizedAnimatedSilhouetteAppliedGeneration();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern uint DMN_GetProductionSizedAnimatedSilhouetteGenerationEvaluationCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern uint DMN_GetProductionSizedAnimatedSilhouetteRegionBuildCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern uint DMN_GetProductionSizedAnimatedSilhouetteApplyMessagePostCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern uint DMN_GetProductionSizedAnimatedSilhouetteApplyExecutionCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern uint DMN_GetProductionSizedAnimatedSilhouetteApplySuccessCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern uint DMN_GetProductionSizedAnimatedSilhouetteApplyFailureCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern uint DMN_GetProductionSizedAnimatedSilhouetteDuplicateMaskSkipCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern uint DMN_GetProductionSizedAnimatedSilhouetteSupersededGenerationCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern uint DMN_GetProductionSizedAnimatedSilhouetteMaximumPendingApplyMessageCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern uint DMN_GetProductionSizedAnimatedSilhouetteMaximumPendingRegionCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern uint DMN_GetProductionSizedAnimatedSilhouettePendingHrgnReplacementCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern uint DMN_GetProductionSizedAnimatedSilhouetteSupersededBuiltRegionCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern uint DMN_GetProductionSizedAnimatedSilhouetteSupersededBuiltHrgnDeletedCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern uint DMN_GetProductionSizedAnimatedSilhouettePhaseTransitionCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern uint DMN_GetProductionSizedAnimatedSilhouetteSuccessfulPhaseApplyCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int DMN_DidProductionSizedAnimatedSilhouettePhaseApply(int p);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern ulong DMN_GetProductionSizedAnimatedSilhouettePhaseLastAppliedGeneration(int p);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern uint DMN_GetProductionSizedAnimatedSilhouettePhaseRawRuns(int p);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern uint DMN_GetProductionSizedAnimatedSilhouettePhaseMergedRectangles(int p);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern uint DMN_GetProductionSizedAnimatedSilhouettePhaseFinalRectangles(int p);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern uint DMN_GetProductionSizedAnimatedSilhouettePhaseCoveredPixels(int p);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern ulong DMN_GetProductionSizedAnimatedSilhouetteMinimumBuildMicroseconds();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern ulong DMN_GetProductionSizedAnimatedSilhouetteMaximumBuildMicroseconds();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern ulong DMN_GetProductionSizedAnimatedSilhouetteAverageBuildMicroseconds();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern ulong DMN_GetProductionSizedAnimatedSilhouetteMinimumSetWindowRgnMicroseconds();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern ulong DMN_GetProductionSizedAnimatedSilhouetteMaximumSetWindowRgnMicroseconds();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern ulong DMN_GetProductionSizedAnimatedSilhouetteAverageSetWindowRgnMicroseconds();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern uint DMN_GetProductionSizedAnimatedSilhouetteHrgnCreatedCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern uint DMN_GetProductionSizedAnimatedSilhouetteHrgnCallerDeletedCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern uint DMN_GetProductionSizedAnimatedSilhouetteHrgnOwnershipTransferredCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern uint DMN_GetProductionSizedAnimatedSilhouetteHrgnValidationCopyDeletedCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int DMN_GetProductionSizedAnimatedSilhouetteHrgnLiveOwnedCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern uint DMN_GetProductionSizedAnimatedSilhouetteInitialGdiObjectCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern uint DMN_GetProductionSizedAnimatedSilhouetteMinimumAnimationGdiObjectCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern uint DMN_GetProductionSizedAnimatedSilhouetteMaximumAnimationGdiObjectCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern uint DMN_GetProductionSizedAnimatedSilhouetteLastAnimationGdiObjectCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int DMN_DidProductionSizedAnimatedSilhouetteInitialRegionRestoreSucceed();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int DMN_DidProductionSizedAnimatedSilhouetteInitialStyleRestoreSucceed();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int DMN_DidProductionSizedAnimatedSilhouetteCounterInvariantsSucceed();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int DMN_DidProductionSizedAnimatedSilhouetteRepresentativeValidationSucceed();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int DMN_DidProductionSizedAnimatedSilhouetteAllPhasesApply();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int DMN_DidProductionSizedAnimatedSilhouettePhaseComplexityDiffer();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int DMN_DidProductionSizedAnimatedSilhouetteHrgnOwnershipInvariantsSucceed();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int DMN_GetContinuousCompositionState();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern uint DMN_GetContinuousCompositionPresentCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern ulong DMN_GetContinuousCompositionElapsedMilliseconds();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int DMN_GetContinuousCompositionLastPresentHRESULT();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int DMN_GetContinuousCompositionLastDeviceRemovedReason();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int DMN_DidContinuousCompositionCompleteNormally();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int DMN_RequestContinuousCompositionDiagnosticsStop();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int DMN_RequestCompositionDiagnosticsShutdown();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int DMN_FinalizeCompositionDiagnosticsThread();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern uint DMN_GetCurrentProcessGdiObjectCount();
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Select()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            DesktopMascotAnimatedWindowRegionDiagnostics.AutoStartEnabled=false;
            DesktopMascotStaticComplexSilhouetteDiagnostics.AutoStartEnabled=false;
            DesktopMascotProductionSizedStaticSilhouetteDiagnostics.AutoStartEnabled=false;
#endif
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Launch()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var go=new GameObject(nameof(DesktopMascotProductionSizedAnimatedSilhouetteDiagnostics));
            DontDestroyOnLoad(go); go.AddComponent<DesktopMascotProductionSizedAnimatedSilhouetteDiagnostics>();
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        void Awake(){previousBackground=Application.runInBackground;Application.runInBackground=true;}
        IEnumerator Start()
        {
            yield return null; started=Time.realtimeSinceStartup;
            while(!TimedOut()&&!Ready())yield return null;
            DesktopMascotContinuousCompositionDiagnostics.TargetFpsForNextRun=30;
            DesktopMascotContinuousCompositionDiagnostics.ProductionAnimatedSilhouettePatternForNextRun=true;
            DesktopMascotContinuousCompositionDiagnostics.AnimatedAlphaPhaseDurationForNextRun=2;
            DesktopMascotContinuousCompositionDiagnostics.ExternalShutdownOwnerForNextRun=true;
            DMN_SetContinuousCompositionTargetFrameCountForNextRun(TargetPresents);
            var go=new GameObject(nameof(DesktopMascotContinuousCompositionDiagnostics));DontDestroyOnLoad(go);go.AddComponent<DesktopMascotContinuousCompositionDiagnostics>();
            while(!TimedOut()&&(source=DesktopMascotContinuousCompositionDiagnostics.ActiveSourceTexture)==null)yield return null;
            pin=GCHandle.Alloc(mask,GCHandleType.Pinned);
            if(DMN_StartAnimatedCompositionAlphaMaskDiagnostics(Size,Size,Size,Threshold)!=1){managedFailure=8;yield return Shutdown();yield break;}
            nextReadback=Time.realtimeSinceStartup;
            while(generation==0&&!TimedOut()){Pump();yield return null;}
            var start=DMN_StartProductionSizedAnimatedSilhouetteDiagnostics(Size,Size,Threshold,30,TargetPresents,PublishMs,PhaseMs);
            Log($"Start result: {start}");Log("Width: 256");Log("Height: 256");Log("Threshold: 128");Log("Mask publish interval milliseconds: 250");Log("Phase duration milliseconds: 2000");Log("Visual duration seconds: 32");Log("Requested target FPS: 30");Log("Effective target FPS: 30");Log("Target Present count: 1200");Log($"Preview uses validated UV transform: {DesktopMascotValidatedPreview.UsesValidatedUvTransform}");
            visualStarted=Time.realtimeSinceStartup;
            while(!TimedOut()&&Time.realtimeSinceStartup-visualStarted<VisualSeconds&&DMN_GetProductionSizedAnimatedSilhouetteFailureStage()==0)
            {
                Pump();DMN_PollProductionSizedAnimatedSilhouetteDiagnostics();
                var phase=DMN_GetProductionSizedAnimatedSilhouetteAppliedPhase();
                if(phase>=0&&phase!=lastPhase){LogPhase(phase);lastPhase=phase;}
                yield return null;
            }
            shuttingDown=true;while(pending&&!TimedOut())yield return null;
            for(int i=0;i<120&&!TimedOut();i++){DMN_PollProductionSizedAnimatedSilhouetteDiagnostics();yield return null;}
            DMN_CompleteProductionSizedAnimatedSilhouetteDiagnostics();DMN_StopProductionSizedAnimatedSilhouetteDiagnostics();
            while(!TimedOut()&&DMN_GetProductionSizedAnimatedSilhouetteState()!=9&&DMN_GetProductionSizedAnimatedSilhouetteFailureStage()==0)yield return null;
            DMN_StopCompositionAlphaMaskDiagnostics();
            while(!TimedOut()&&DMN_GetContinuousCompositionState()!=10&&DMN_GetContinuousCompositionState()!=13)yield return null;
            DMN_RequestContinuousCompositionDiagnosticsStop();
            while(!TimedOut()&&DMN_GetContinuousCompositionState()!=12)yield return null;
            DMN_RequestCompositionDiagnosticsShutdown();
            while(!TimedOut()&&DMN_GetCompositionInitializationState()!=17)yield return null;
            DMN_FinalizeCompositionDiagnosticsThread();
            for(int i=0;i<5;i++){finalGdi[i]=DMN_GetCurrentProcessGdiObjectCount();if(i<4)yield return new WaitForSecondsRealtime(.1f);}
            LogFinal();yield return Shutdown();
        }
        void Pump()
        {
            if(pending||Time.realtimeSinceStartup<nextReadback||shuttingDown)return;
            pending=true;requestPhase=DesktopMascotContinuousCompositionDiagnostics.CurrentAnimatedAlphaPhase;
            nextReadback=Time.realtimeSinceStartup+.25f;AsyncGPUReadback.Request(source,0,OnReadback);
        }
        void OnReadback(AsyncGPUReadbackRequest r)
        {
            pending=false;if(r.hasError){readbackErrors++;managedFailure=9;return;}
            var data=r.GetData<byte>();
            if(!orientationKnown)
            {
                bool normal=Matches(data,false,requestPhase),flipped=Matches(data,true,requestPhase);
                if(normal==flipped){managedFailure=13;return;}flipRows=flipped;orientationKnown=true;
            }
            for(int y=0;y<Size;y++){int sy=flipRows?Size-1-y:y;for(int x=0;x<Size;x++)mask[y*Size+x]=data[(sy*Size+x)*4+3];}
            generation++;if(DMN_SubmitCompositionAlphaMask(pin.AddrOfPinnedObject(),Size,Size,Size,generation)!=1)managedFailure=9;
        }
        static bool Matches(NativeArray<byte>d,bool flip,int phase)
        {
            bool Hit(int x,int y){int sy=flip?Size-1-y:y;return d[(sy*Size+x)*4+3]>=Threshold;}
            bool common=Hit(124,152)&&Hit(60,32)&&!Hit(248,8);
            bool n=Hit(60,160),r=Hit(52,104),low=Hit(228,172),up=Hit(228,116),normal=Hit(124,120),legs=Hit(68,224)&&Hit(176,224);
            return common&&(phase==0?n&&!r&&low&&!up&&normal:phase==1?!n&&r&&low&&!up:phase==2?n&&!r&&!low&&up:!normal&&legs);
        }
        static void LogPhase(int p)=>Log($"Phase applied: {p}; generation: {DMN_GetProductionSizedAnimatedSilhouetteAppliedGeneration()}; raw: {DMN_GetProductionSizedAnimatedSilhouettePhaseRawRuns(p)}; merged: {DMN_GetProductionSizedAnimatedSilhouettePhaseMergedRectangles(p)}; final: {DMN_GetProductionSizedAnimatedSilhouettePhaseFinalRectangles(p)}; covered: {DMN_GetProductionSizedAnimatedSilhouettePhaseCoveredPixels(p)}");
        void LogFinal()
        {
            var sorted=(uint[])finalGdi.Clone();Array.Sort(sorted);long delta=(long)sorted[2]-DMN_GetProductionSizedAnimatedSilhouetteInitialGdiObjectCount();var elapsed=DMN_GetContinuousCompositionElapsedMilliseconds();var fps=elapsed==0?0:DMN_GetContinuousCompositionPresentCount()*1000.0/elapsed;
            var builds=DMN_GetProductionSizedAnimatedSilhouetteRegionBuildCount();
            var posts=DMN_GetProductionSizedAnimatedSilhouetteApplyMessagePostCount();
            var executions=DMN_GetProductionSizedAnimatedSilhouetteApplyExecutionCount();
            var successes=DMN_GetProductionSizedAnimatedSilhouetteApplySuccessCount();
            var failures=DMN_GetProductionSizedAnimatedSilhouetteApplyFailureCount();
            bool stable=finalGdi[0]==finalGdi[1]&&finalGdi[1]==finalGdi[2]&&finalGdi[2]==finalGdi[3]&&finalGdi[3]==finalGdi[4];
            bool phasesValid=true;
            for(int phase=0;phase<4;phase++)
            {
                uint covered=DMN_GetProductionSizedAnimatedSilhouettePhaseCoveredPixels(phase);
                phasesValid &= DMN_DidProductionSizedAnimatedSilhouettePhaseApply(phase)!=0
                    && covered>0&&covered<Size*Size
                    && DMN_GetProductionSizedAnimatedSilhouettePhaseRawRuns(phase)>1
                    && DMN_GetProductionSizedAnimatedSilhouettePhaseMergedRectangles(phase)>1
                    && DMN_GetProductionSizedAnimatedSilhouettePhaseMergedRectangles(phase)<=DMN_GetProductionSizedAnimatedSilhouettePhaseRawRuns(phase)
                    && DMN_GetProductionSizedAnimatedSilhouettePhaseFinalRectangles(phase)>1;
            }
            var published=DMN_GetProductionSizedAnimatedSilhouettePublishedGeneration();
            bool automated=DMN_GetProductionSizedAnimatedSilhouetteState()==9
                && DMN_GetProductionSizedAnimatedSilhouetteFailureStage()==0
                && published>=120&&published<=132
                && DMN_GetProductionSizedAnimatedSilhouetteAppliedGeneration()<=published
                && (builds==16||builds==17)
                && DMN_GetProductionSizedAnimatedSilhouetteDuplicateMaskSkipCount()>80
                && successes+failures==executions&&executions==posts
                && successes>=16&&failures==0
                && DMN_GetProductionSizedAnimatedSilhouetteMaximumPendingApplyMessageCount()<=1
                && DMN_GetProductionSizedAnimatedSilhouetteMaximumPendingRegionCount()<=1
                && DMN_GetProductionSizedAnimatedSilhouetteSupersededBuiltRegionCount()==DMN_GetProductionSizedAnimatedSilhouetteSupersededBuiltHrgnDeletedCount()
                && phasesValid
                && DMN_DidProductionSizedAnimatedSilhouetteCounterInvariantsSucceed()!=0
                && DMN_DidProductionSizedAnimatedSilhouetteRepresentativeValidationSucceed()!=0
                && DMN_DidProductionSizedAnimatedSilhouetteAllPhasesApply()!=0
                && DMN_DidProductionSizedAnimatedSilhouettePhaseComplexityDiffer()!=0
                && DMN_GetProductionSizedAnimatedSilhouetteMaximumBuildMicroseconds()<=10000
                && DMN_GetProductionSizedAnimatedSilhouetteAverageBuildMicroseconds()<=2000
                && DMN_GetProductionSizedAnimatedSilhouetteMaximumSetWindowRgnMicroseconds()<=5000
                && DMN_GetProductionSizedAnimatedSilhouetteAverageSetWindowRgnMicroseconds()<=2000
                && DMN_GetProductionSizedAnimatedSilhouetteMaximumAnimationGdiObjectCount()<=DMN_GetProductionSizedAnimatedSilhouetteInitialGdiObjectCount()+5
                && stable&&delta<=2
                && DMN_GetProductionSizedAnimatedSilhouetteHrgnLiveOwnedCount()==0
                && DMN_DidProductionSizedAnimatedSilhouetteHrgnOwnershipInvariantsSucceed()!=0
                && DMN_GetContinuousCompositionPresentCount()==TargetPresents
                && fps>=25&&fps<=35
                && DMN_GetContinuousCompositionLastPresentHRESULT()==0
                && DMN_GetContinuousCompositionLastDeviceRemovedReason()==0
                && DMN_DidContinuousCompositionCompleteNormally()!=0
                && DMN_DidReadbackExpectedOrientationMatch()!=0
                && DesktopMascotValidatedPreview.UsesValidatedUvTransform
                && managedFailure==0&&readbackErrors==0
                && DMN_DidProductionSizedAnimatedSilhouetteInitialRegionRestoreSucceed()!=0
                && DMN_DidProductionSizedAnimatedSilhouetteInitialStyleRestoreSucceed()!=0;
            if(!automated&&managedFailure==0)managedFailure=30;
            Log($"State: {DMN_GetProductionSizedAnimatedSilhouetteState()}");Log($"Failure stage: {DMN_GetProductionSizedAnimatedSilhouetteFailureStage()}");Log($"Published generation count: {DMN_GetProductionSizedAnimatedSilhouettePublishedGeneration()}");Log($"Applied generation: {DMN_GetProductionSizedAnimatedSilhouetteAppliedGeneration()}");
            Log($"Generation evaluation count: {DMN_GetProductionSizedAnimatedSilhouetteGenerationEvaluationCount()}");Log($"Region build count: {DMN_GetProductionSizedAnimatedSilhouetteRegionBuildCount()}");Log($"Duplicate mask skip count: {DMN_GetProductionSizedAnimatedSilhouetteDuplicateMaskSkipCount()}");Log($"Superseded generation count: {DMN_GetProductionSizedAnimatedSilhouetteSupersededGenerationCount()}");
            Log($"Region apply message post/execution/success/failure: {DMN_GetProductionSizedAnimatedSilhouetteApplyMessagePostCount()}/{DMN_GetProductionSizedAnimatedSilhouetteApplyExecutionCount()}/{DMN_GetProductionSizedAnimatedSilhouetteApplySuccessCount()}/{DMN_GetProductionSizedAnimatedSilhouetteApplyFailureCount()}");
            Log($"Maximum pending apply message/region count: {DMN_GetProductionSizedAnimatedSilhouetteMaximumPendingApplyMessageCount()}/{DMN_GetProductionSizedAnimatedSilhouetteMaximumPendingRegionCount()}");
            Log($"Pending HRGN replacement/superseded built/superseded deleted: {DMN_GetProductionSizedAnimatedSilhouettePendingHrgnReplacementCount()}/{DMN_GetProductionSizedAnimatedSilhouetteSupersededBuiltRegionCount()}/{DMN_GetProductionSizedAnimatedSilhouetteSupersededBuiltHrgnDeletedCount()}");
            for(int p=0;p<4;p++)Log($"Phase {p} applied: {DMN_DidProductionSizedAnimatedSilhouettePhaseApply(p)!=0}; last generation: {DMN_GetProductionSizedAnimatedSilhouettePhaseLastAppliedGeneration(p)}; raw/merged/final/covered: {DMN_GetProductionSizedAnimatedSilhouettePhaseRawRuns(p)}/{DMN_GetProductionSizedAnimatedSilhouettePhaseMergedRectangles(p)}/{DMN_GetProductionSizedAnimatedSilhouettePhaseFinalRectangles(p)}/{DMN_GetProductionSizedAnimatedSilhouettePhaseCoveredPixels(p)}");
            Log($"Build min/max/average microseconds: {DMN_GetProductionSizedAnimatedSilhouetteMinimumBuildMicroseconds()}/{DMN_GetProductionSizedAnimatedSilhouetteMaximumBuildMicroseconds()}/{DMN_GetProductionSizedAnimatedSilhouetteAverageBuildMicroseconds()}");Log($"SetWindowRgn min/max/average microseconds: {DMN_GetProductionSizedAnimatedSilhouetteMinimumSetWindowRgnMicroseconds()}/{DMN_GetProductionSizedAnimatedSilhouetteMaximumSetWindowRgnMicroseconds()}/{DMN_GetProductionSizedAnimatedSilhouetteAverageSetWindowRgnMicroseconds()}");
            Log($"GDI initial/min/max/last: {DMN_GetProductionSizedAnimatedSilhouetteInitialGdiObjectCount()}/{DMN_GetProductionSizedAnimatedSilhouetteMinimumAnimationGdiObjectCount()}/{DMN_GetProductionSizedAnimatedSilhouetteMaximumAnimationGdiObjectCount()}/{DMN_GetProductionSizedAnimatedSilhouetteLastAnimationGdiObjectCount()}");Log($"Final GDI samples: [{string.Join(", ",finalGdi)}]; stabilized delta: {delta}");
            Log($"HRGN created/deleted/transferred/validation/live: {DMN_GetProductionSizedAnimatedSilhouetteHrgnCreatedCount()}/{DMN_GetProductionSizedAnimatedSilhouetteHrgnCallerDeletedCount()}/{DMN_GetProductionSizedAnimatedSilhouetteHrgnOwnershipTransferredCount()}/{DMN_GetProductionSizedAnimatedSilhouetteHrgnValidationCopyDeletedCount()}/{DMN_GetProductionSizedAnimatedSilhouetteHrgnLiveOwnedCount()}");
            Log($"Counter/representative/all phases/complexity/HRGN invariants: {DMN_DidProductionSizedAnimatedSilhouetteCounterInvariantsSucceed()!=0}/{DMN_DidProductionSizedAnimatedSilhouetteRepresentativeValidationSucceed()!=0}/{DMN_DidProductionSizedAnimatedSilhouetteAllPhasesApply()!=0}/{DMN_DidProductionSizedAnimatedSilhouettePhaseComplexityDiffer()!=0}/{DMN_DidProductionSizedAnimatedSilhouetteHrgnOwnershipInvariantsSucceed()!=0}");
            Log($"Present count: {DMN_GetContinuousCompositionPresentCount()}; elapsed: {elapsed}; measured FPS: {fps:F2}; HRESULT: {DMN_GetContinuousCompositionLastPresentHRESULT():X8}; removed: {DMN_GetContinuousCompositionLastDeviceRemovedReason():X8}; successful: {DMN_DidContinuousCompositionCompleteNormally()!=0}");
            Log($"Initial region/style restored: {DMN_DidProductionSizedAnimatedSilhouetteInitialRegionRestoreSucceed()!=0}/{DMN_DidProductionSizedAnimatedSilhouetteInitialStyleRestoreSucceed()!=0}");Log($"GPU/readback expected orientation match: {DMN_DidReadbackExpectedOrientationMatch()!=0}");Log($"GPU/readback rows normalized from vertical flip: {flipRows}");Log($"Preview uses validated UV transform: {DesktopMascotValidatedPreview.UsesValidatedUvTransform}");Log($"Automated diagnostics passed: {automated}");Log("Visual verification pending: True");
        }
        IEnumerator Shutdown(){Application.runInBackground=previousBackground;if(pin.IsAllocated&&!pending)pin.Free();Log($"Managed failure stage: {managedFailure}");Log($"Readback errors: {readbackErrors}");Log($"runInBackground restored: {Application.runInBackground==previousBackground}");yield return null;Application.Quit();}
        void OnGUI(){if(source==null)return;float s=Mathf.Min(1.5f,(Screen.height-120f)/Size);DesktopMascotValidatedPreview.DrawValidatedTopLeftPreview(new Rect((Screen.width-Size*s)/2,100,Size*s,Size*s),source);GUI.Label(new Rect(10,5,Screen.width-20,90),$"Production-Sized Animated Silhouette Diagnostics\nPhase {DMN_GetProductionSizedAnimatedSilhouetteAppliedPhase()}  Generation {generation}\nPresent {DMN_GetContinuousCompositionPresentCount()}/{TargetPresents}");}
        bool TimedOut()=>Time.realtimeSinceStartup-started>=TimeoutSeconds;
        static bool Ready()=>DMN_GetCompositionInitializationState()==14&&DMN_IsDestinationTextureAvailable()!=0&&DMN_WasReadbackValidationCompleted()!=0;
        static void Log(string s)=>Debug.Log($"[DesktopMascotProductionSizedAnimatedSilhouetteDiagnostics] {s}");
#endif
    }
}
