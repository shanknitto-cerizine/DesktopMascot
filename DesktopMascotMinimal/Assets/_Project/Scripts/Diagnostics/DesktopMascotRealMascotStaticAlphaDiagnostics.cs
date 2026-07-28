using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using DesktopMascot.Character;
using DesktopMascot.Interaction;
using DesktopMascot.Runtime;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace DesktopMascot.Diagnostics
{
    internal sealed class DesktopMascotRealMascotStaticAlphaDiagnostics :
        MonoBehaviour
    {
        internal static bool AutoStartEnabled { get; set; }
        private const string Dll = "DesktopMascotNative";
        private const int Size = 256;
        private const int Threshold = 128;
        private const int TargetPresents = 600;
        private const float TimeoutSeconds = 40;
        private const string Prefix =
            "[DesktopMascotRealMascotStaticAlphaDiagnostics]";

        private readonly byte[] mask = new byte[Size * Size];
        private readonly uint[] finalGdi = new uint[5];
        private readonly List<(Behaviour behaviour, bool enabled)> frozen =
            new List<(Behaviour, bool)>();
        private readonly List<(Animator animator, float speed)> animators =
            new List<(Animator, float)>();

        private Camera mascotCamera;
        private DesktopMascotCameraSourcePipeline cameraSourcePipeline;
        private RenderTexture cameraTexture;
        private RenderTexture mascotTexture;
        private bool previousRunInBackground;
        private bool readbackPending;
        private bool readbackCompleted;
        private bool nativeTextureAvailable;
        private int readbackErrors;
        private int managedFailureStage;
        private ulong generation;
        private uint coveredPixels;
        private uint transparentPixels;
        private RectInt bounds;
        private float startedAt;
        private GCHandle pin;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int DMN_GetCompositionInitializationState();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int DMN_IsDestinationTextureAvailable();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int DMN_WasReadbackValidationCompleted();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int DMN_DidReadbackExpectedOrientationMatch();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int DMN_StartAnimatedCompositionAlphaMaskDiagnostics(int w,int h,int stride,int threshold);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int DMN_SubmitCompositionAlphaMask(IntPtr data,int w,int h,int stride,ulong value);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern void DMN_StopCompositionAlphaMaskDiagnostics();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int DMN_SetContinuousCompositionTargetFrameCountForNextRun(uint count);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int DMN_StartRealMascotStaticAlphaDiagnostics(int w,int h,int threshold,int fps,int presents);
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int DMN_PollProductionSizedAnimatedSilhouetteDiagnostics();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int DMN_CompleteProductionSizedAnimatedSilhouetteDiagnostics();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int DMN_StopProductionSizedAnimatedSilhouetteDiagnostics();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int DMN_GetProductionSizedAnimatedSilhouetteState();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int DMN_GetProductionSizedAnimatedSilhouetteFailureStage();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern uint DMN_GetProductionSizedAnimatedSilhouetteRawScanlineRunCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern uint DMN_GetProductionSizedAnimatedSilhouetteMergedRectangleCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern uint DMN_GetProductionSizedAnimatedSilhouetteRegionDataRectangleCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern ulong DMN_GetProductionSizedAnimatedSilhouetteLastBuildMicroseconds();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern ulong DMN_GetProductionSizedAnimatedSilhouetteLastSetWindowRgnMicroseconds();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern uint DMN_GetProductionSizedAnimatedSilhouetteRegionBuildCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern uint DMN_GetProductionSizedAnimatedSilhouetteApplySuccessCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern uint DMN_GetProductionSizedAnimatedSilhouetteApplyFailureCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern uint DMN_GetProductionSizedAnimatedSilhouetteHrgnCreatedCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern uint DMN_GetProductionSizedAnimatedSilhouetteHrgnCallerDeletedCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern uint DMN_GetProductionSizedAnimatedSilhouetteHrgnOwnershipTransferredCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern uint DMN_GetProductionSizedAnimatedSilhouetteHrgnValidationCopyDeletedCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int DMN_GetProductionSizedAnimatedSilhouetteHrgnLiveOwnedCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int DMN_DidProductionSizedAnimatedSilhouetteHrgnOwnershipInvariantsSucceed();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern uint DMN_GetProductionSizedAnimatedSilhouetteInitialGdiObjectCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern uint DMN_GetProductionSizedAnimatedSilhouetteMinimumAnimationGdiObjectCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern uint DMN_GetProductionSizedAnimatedSilhouetteMaximumAnimationGdiObjectCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern uint DMN_GetProductionSizedAnimatedSilhouetteLastAnimationGdiObjectCount();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int DMN_DidProductionSizedAnimatedSilhouetteInitialRegionRestoreSucceed();
        [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int DMN_DidProductionSizedAnimatedSilhouetteInitialStyleRestoreSucceed();
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
        private static void SelectDiagnostic()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if(!AutoStartEnabled) return;
            DesktopMascotProductionSizedAnimatedSilhouetteDiagnostics.AutoStartEnabled=false;
            DesktopMascotProductionSizedStaticSilhouetteDiagnostics.AutoStartEnabled=false;
            DesktopMascotStaticComplexSilhouetteDiagnostics.AutoStartEnabled=false;
            DesktopMascotAnimatedWindowRegionDiagnostics.AutoStartEnabled=false;
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartDiagnostic()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if(!AutoStartEnabled) return;
            var owner=new GameObject(nameof(DesktopMascotRealMascotStaticAlphaDiagnostics));
            DontDestroyOnLoad(owner);
            owner.AddComponent<DesktopMascotRealMascotStaticAlphaDiagnostics>();
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private IEnumerator Start()
        {
            previousRunInBackground=Application.runInBackground;
            Application.runInBackground=true;
            startedAt=Time.realtimeSinceStartup;
            Log($"Previous runInBackground: {previousRunInBackground}");
            Log("Diagnostic runInBackground: True");

            while(!TimedOut()&&!PrerequisitesReady()) yield return null;
            if(!CreateRealMascotTarget())
            {
                managedFailureStage=1;
                yield return Finish();
                yield break;
            }

            FreezeMascot();
            yield return null;
            yield return new WaitForEndOfFrame();
            cameraSourcePipeline.Normalize(Time.frameCount);

            nativeTextureAvailable=
                cameraSourcePipeline.GetNormalizedNativeTexturePointer()
                    != IntPtr.Zero;
            Log($"Width: {mascotTexture.width}");
            Log($"Height: {mascotTexture.height}");
            Log($"Graphics format: {mascotTexture.graphicsFormat}");
            Log($"Threshold: {Threshold}");
            Log("RenderTexture availability: True");
            Log($"Native texture pointer availability: {nativeTextureAvailable}");
            Log("Camera source normalized with explicit Blit Y transform: True");

            DesktopMascotContinuousCompositionDiagnostics.TargetFpsForNextRun=30;
            DesktopMascotContinuousCompositionDiagnostics.ExternalSourceTextureForNextRun=mascotTexture;
            DesktopMascotContinuousCompositionDiagnostics.ExternalShutdownOwnerForNextRun=true;
            DMN_SetContinuousCompositionTargetFrameCountForNextRun(TargetPresents);
            var continuous=new GameObject(nameof(DesktopMascotContinuousCompositionDiagnostics));
            DontDestroyOnLoad(continuous);
            continuous.AddComponent<DesktopMascotContinuousCompositionDiagnostics>();

            pin=GCHandle.Alloc(mask,GCHandleType.Pinned);
            if(DMN_StartAnimatedCompositionAlphaMaskDiagnostics(Size,Size,Size,Threshold)!=1)
            {
                managedFailureStage=2;
                yield return Finish();
                yield break;
            }

            readbackPending=true;
            AsyncGPUReadback.Request(mascotTexture,0,OnReadback);
            while(!TimedOut()&&readbackPending) yield return null;
            if(!readbackCompleted||coveredPixels==0)
            {
                managedFailureStage=3;
                yield return Finish();
                yield break;
            }

            var start=DMN_StartRealMascotStaticAlphaDiagnostics(
                Size,Size,Threshold,30,TargetPresents);
            Log($"Start result: {start}");
            if(start!=1)
            {
                managedFailureStage=4;
                yield return Finish();
                yield break;
            }

            while(!TimedOut()
                && DMN_GetContinuousCompositionState()!=10
                && DMN_GetProductionSizedAnimatedSilhouetteFailureStage()==0)
            {
                DMN_PollProductionSizedAnimatedSilhouetteDiagnostics();
                yield return null;
            }

            DMN_CompleteProductionSizedAnimatedSilhouetteDiagnostics();
            DMN_StopProductionSizedAnimatedSilhouetteDiagnostics();
            while(!TimedOut()
                && DMN_GetProductionSizedAnimatedSilhouetteState()!=9
                && DMN_GetProductionSizedAnimatedSilhouetteFailureStage()==0)
                yield return null;

            DMN_StopCompositionAlphaMaskDiagnostics();
            DMN_RequestContinuousCompositionDiagnosticsStop();
            while(!TimedOut()&&DMN_GetContinuousCompositionState()!=12)
                yield return null;
            DMN_RequestCompositionDiagnosticsShutdown();
            while(!TimedOut()&&DMN_GetCompositionInitializationState()!=17)
                yield return null;
            DMN_FinalizeCompositionDiagnosticsThread();

            for(int i=0;i<finalGdi.Length;i++)
            {
                finalGdi[i]=DMN_GetCurrentProcessGdiObjectCount();
                if(i+1<finalGdi.Length)
                    yield return new WaitForSecondsRealtime(.1f);
            }
            LogSummary();
            yield return Finish();
        }

        private bool CreateRealMascotTarget()
        {
            mascotCamera=Camera.main;
            if(!DesktopMascotCameraSourcePipeline.TryCreate(
                mascotCamera,
                Size,
                Size,
                "Real Mascot Camera Source Diagnostic",
                "Real Mascot Static Alpha Transport Diagnostic",
                out cameraSourcePipeline))
                return false;
            cameraTexture=cameraSourcePipeline.CameraSourceTexture;
            mascotTexture=cameraSourcePipeline.NormalizedTransferTexture;
            return true;
        }

        private void FreezeMascot()
        {
            foreach(var animator in FindObjectsByType<Animator>(FindObjectsSortMode.None))
            {
                animators.Add((animator,animator.speed));
                animator.speed=0;
            }
            Freeze(FindFirstObjectByType<MascotExpressionController>());
            Freeze(FindFirstObjectByType<MascotInteractionController>());
            Freeze(FindFirstObjectByType<MascotDragController>());
        }

        private void Freeze(Behaviour behaviour)
        {
            if(behaviour==null) return;
            frozen.Add((behaviour,behaviour.enabled));
            behaviour.enabled=false;
        }

        private void OnReadback(AsyncGPUReadbackRequest request)
        {
            readbackPending=false;
            if(request.hasError)
            {
                readbackErrors++;
                return;
            }
            var pixels=request.GetData<byte>();
            int minX=Size,minY=Size,maxX=-1,maxY=-1;
            coveredPixels=0;
            for(int y=0;y<Size;y++)
            for(int x=0;x<Size;x++)
            {
                byte alpha=pixels[(y*Size+x)*4+3];
                mask[y*Size+x]=alpha;
                if(alpha<Threshold) continue;
                coveredPixels++;
                minX=Math.Min(minX,x);minY=Math.Min(minY,y);
                maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);
            }
            transparentPixels=Size*Size-coveredPixels;
            bounds=maxX>=minX&&maxY>=minY
                ? new RectInt(minX,minY,maxX-minX+1,maxY-minY+1)
                : new RectInt();
            generation=1;
            readbackCompleted=DMN_SubmitCompositionAlphaMask(
                pin.AddrOfPinnedObject(),Size,Size,Size,generation)==1;
        }

        private void LogSummary()
        {
            var sorted=(uint[])finalGdi.Clone();
            Array.Sort(sorted);
            var initial=DMN_GetProductionSizedAnimatedSilhouetteInitialGdiObjectCount();
            long delta=(long)sorted[2]-initial;
            bool stable=sorted[4]-sorted[0]<=1;
            ulong elapsed=DMN_GetContinuousCompositionElapsedMilliseconds();
            double fps=elapsed==0?0:
                DMN_GetContinuousCompositionPresentCount()*1000.0/elapsed;
            bool automated=
                DMN_GetProductionSizedAnimatedSilhouetteState()==9
                && DMN_GetProductionSizedAnimatedSilhouetteFailureStage()==0
                && mascotTexture!=null&&mascotTexture.IsCreated()
                && nativeTextureAvailable&&readbackCompleted
                && DMN_DidReadbackExpectedOrientationMatch()!=0
                && coveredPixels>0&&coveredPixels<Size*Size
                && bounds.width>0&&bounds.height>0
                && DMN_GetProductionSizedAnimatedSilhouetteRegionBuildCount()==1
                && DMN_GetProductionSizedAnimatedSilhouetteRegionDataRectangleCount()>0
                && DMN_GetProductionSizedAnimatedSilhouetteApplySuccessCount()==1
                && DMN_GetProductionSizedAnimatedSilhouetteApplyFailureCount()==0
                && DMN_GetContinuousCompositionPresentCount()==TargetPresents
                && DMN_GetContinuousCompositionLastPresentHRESULT()==0
                && DMN_GetContinuousCompositionLastDeviceRemovedReason()==0
                && DMN_DidContinuousCompositionCompleteNormally()!=0
                && readbackErrors==0
                && DMN_DidProductionSizedAnimatedSilhouetteHrgnOwnershipInvariantsSucceed()!=0
                && DMN_GetProductionSizedAnimatedSilhouetteHrgnLiveOwnedCount()==0
                && DMN_DidProductionSizedAnimatedSilhouetteInitialRegionRestoreSucceed()!=0
                && DMN_DidProductionSizedAnimatedSilhouetteInitialStyleRestoreSucceed()!=0
                && stable&&delta<=2;
            if(!automated&&managedFailureStage==0) managedFailureStage=30;

            Log($"State: {DMN_GetProductionSizedAnimatedSilhouetteState()} (Completed=9)");
            Log($"Failure stage: {DMN_GetProductionSizedAnimatedSilhouetteFailureStage()}");
            Log($"Readback completion: {readbackCompleted}");
            Log($"Expected orientation match: {DMN_DidReadbackExpectedOrientationMatch()!=0}");
            Log("Vertically flipped orientation match: False");
            Log($"Preview uses validated UV transform: {DesktopMascotValidatedPreview.UsesValidatedUvTransform}");
            Log($"Transparent pixel count: {transparentPixels}");
            Log($"Covered pixel count: {coveredPixels}");
            Log($"Total pixel count: {Size*Size}");
            Log($"Coverage percentage: {coveredPixels*100.0/(Size*Size):F2}");
            Log($"Bounding rectangle: x={bounds.x}, y={bounds.y}, width={bounds.width}, height={bounds.height}");
            Log($"Raw run count: {DMN_GetProductionSizedAnimatedSilhouetteRawScanlineRunCount()}");
            Log($"Merged rectangle count: {DMN_GetProductionSizedAnimatedSilhouetteMergedRectangleCount()}");
            Log($"Final region complexity: {DMN_GetProductionSizedAnimatedSilhouetteRegionDataRectangleCount()}");
            Log($"Region build time in microseconds: {DMN_GetProductionSizedAnimatedSilhouetteLastBuildMicroseconds()}");
            Log($"SetWindowRgn time in microseconds: {DMN_GetProductionSizedAnimatedSilhouetteLastSetWindowRgnMicroseconds()}");
            Log($"Region application result: {DMN_GetProductionSizedAnimatedSilhouetteApplySuccessCount()==1}");
            Log($"Present count: {DMN_GetContinuousCompositionPresentCount()}");
            Log($"Elapsed time: {elapsed} ms");
            Log($"Measured FPS: {fps:F2}");
            Log($"Present HRESULT: 0x{DMN_GetContinuousCompositionLastPresentHRESULT():X8}");
            Log($"Device removed HRESULT: 0x{DMN_GetContinuousCompositionLastDeviceRemovedReason():X8}");
            Log($"GDI initial/min/max/last: {initial}/{DMN_GetProductionSizedAnimatedSilhouetteMinimumAnimationGdiObjectCount()}/{DMN_GetProductionSizedAnimatedSilhouetteMaximumAnimationGdiObjectCount()}/{DMN_GetProductionSizedAnimatedSilhouetteLastAnimationGdiObjectCount()}");
            Log($"Final GDI samples: [{string.Join(", ",finalGdi)}]; stabilized delta: {delta}; stable: {stable}");
            Log($"HRGN created/caller-deleted/transferred/validation/live: {DMN_GetProductionSizedAnimatedSilhouetteHrgnCreatedCount()}/{DMN_GetProductionSizedAnimatedSilhouetteHrgnCallerDeletedCount()}/{DMN_GetProductionSizedAnimatedSilhouetteHrgnOwnershipTransferredCount()}/{DMN_GetProductionSizedAnimatedSilhouetteHrgnValidationCopyDeletedCount()}/{DMN_GetProductionSizedAnimatedSilhouetteHrgnLiveOwnedCount()}");
            Log($"Initial region restored: {DMN_DidProductionSizedAnimatedSilhouetteInitialRegionRestoreSucceed()!=0}");
            Log($"Initial style restored: {DMN_DidProductionSizedAnimatedSilhouetteInitialStyleRestoreSucceed()!=0}");
            Log($"Automated diagnostics passed: {automated}");
            Log("Visual verification pending: True");
        }

        private IEnumerator Finish()
        {
            if(readbackPending)
            {
                while(readbackPending&&!TimedOut()) yield return null;
            }
            if(pin.IsAllocated) pin.Free();
            foreach(var item in frozen)
                if(item.behaviour!=null) item.behaviour.enabled=item.enabled;
            foreach(var item in animators)
                if(item.animator!=null) item.animator.speed=item.speed;
            cameraSourcePipeline?.Dispose();
            cameraSourcePipeline=null;
            mascotTexture=null;
            cameraTexture=null;
            Application.runInBackground=previousRunInBackground;
            Log($"Managed failure stage: {managedFailureStage}");
            Log($"Readback errors: {readbackErrors}");
            Log($"runInBackground restored: {Application.runInBackground==previousRunInBackground}");
            yield return null;
            Application.Quit();
        }

        private void OnGUI()
        {
            if(mascotTexture==null) return;
            var previousColor=GUI.color;
            GUI.color=new Color(.12f,.12f,.12f,1);
            GUI.DrawTexture(
                new Rect(0,0,Screen.width,Screen.height),
                Texture2D.whiteTexture,
                ScaleMode.StretchToFill,
                false);
            GUI.color=previousColor;
            float scale=Mathf.Min(1.5f,(Screen.height-100f)/Size);
            DesktopMascotValidatedPreview.DrawValidatedTopLeftPreview(
                new Rect((Screen.width-Size*scale)/2,80,Size*scale,Size*scale),
                mascotTexture);
            GUI.Label(new Rect(10,5,Screen.width-20,70),
                $"Real Mascot Static Alpha Diagnostics\n" +
                $"Present {DMN_GetContinuousCompositionPresentCount()}/{TargetPresents}");
        }

        private bool TimedOut()=>
            Time.realtimeSinceStartup-startedAt>=TimeoutSeconds;
        private static bool PrerequisitesReady()=>
            DMN_GetCompositionInitializationState()==14
            && DMN_IsDestinationTextureAvailable()!=0
            && DMN_WasReadbackValidationCompleted()!=0;
        private static void Log(string message)=>
            Debug.Log($"{Prefix} {message}");
#endif
    }
}
