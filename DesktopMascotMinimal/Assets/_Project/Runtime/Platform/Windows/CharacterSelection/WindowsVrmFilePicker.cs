using System;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

namespace DesktopMascot.Runtime.CharacterSelection
{
    internal enum VrmFilePickerStatus
    {
        Success = 0,
        Cancelled = 1,
        AlreadyRunning = 2,
        OwnerUnavailable = 3,
        ComInitializationFailed = 4,
        DialogFailed = 5,
        UnsupportedPlatform = 6
    }

    internal readonly struct VrmFilePickerResult
    {
        internal VrmFilePickerResult(
            VrmFilePickerStatus status,
            string selectedPath,
            string exceptionType)
        {
            Status = status;
            SelectedPath = selectedPath;
            ExceptionType = exceptionType;
        }

        internal VrmFilePickerStatus Status { get; }
        internal string SelectedPath { get; }
        internal string ExceptionType { get; }
        internal bool Succeeded =>
            Status == VrmFilePickerStatus.Success
            && !string.IsNullOrEmpty(SelectedPath);
    }

    internal interface IVrmFilePicker
    {
        bool IsRunning { get; }
        bool TryStart(IntPtr ownerWindow);
        bool TryTakeResult(out VrmFilePickerResult result);
        void BeginShutdown();
    }

    internal sealed class WindowsVrmFilePicker : IVrmFilePicker
    {
        internal const string VrmFilterPattern = "*.vrm";
        private const uint CoinitApartmentThreaded = 0x2;
        private const uint CoinitDisableOle1Dde = 0x4;
        private const int CancelledHResult =
            unchecked((int)0x800704C7);
        private const uint FosNoChangeDirectory = 0x8;
        private const uint FosForceFileSystem = 0x40;
        private const uint FosPathMustExist = 0x800;
        private const uint FosFileMustExist = 0x1000;
        private const uint FosDoNotAddToRecent = 0x2000000;
        private const uint SigDnFileSystemPath = 0x80058000;

        private static readonly Guid FileOpenDialogClassId =
            new("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7");

        private readonly object gate = new();
        private bool running;
        private bool resultAvailable;
        private bool shutdownStarted;
        private VrmFilePickerResult pendingResult;

        public bool IsRunning
        {
            get
            {
                lock (gate)
                    return running;
            }
        }

        public bool TryStart(IntPtr ownerWindow)
        {
            Debug.Log(
                "[DesktopMascotVrmFilePicker] Start request owner " +
                $"available: {ownerWindow != IntPtr.Zero}");
            lock (gate)
            {
                if (shutdownStarted)
                    return false;
            }
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            lock (gate)
            {
                if (running || resultAvailable)
                    return false;
                if (ownerWindow == IntPtr.Zero)
                {
                    pendingResult = Result(
                        VrmFilePickerStatus.OwnerUnavailable);
                    resultAvailable = true;
                    return false;
                }

                running = true;
                var worker = new Thread(() => RunDialog(ownerWindow))
                {
                    IsBackground = true,
                    Name = "DesktopMascotVrmFilePicker"
                };
                worker.SetApartmentState(ApartmentState.STA);
                worker.Start();
                Debug.Log(
                    "[DesktopMascotVrmFilePicker] STA picker thread " +
                    "start requested: True");
                return true;
            }
#else
            lock (gate)
            {
                pendingResult = Result(
                    VrmFilePickerStatus.UnsupportedPlatform);
                resultAvailable = true;
            }
            return false;
#endif
        }

        public bool TryTakeResult(out VrmFilePickerResult result)
        {
            lock (gate)
            {
                if (!resultAvailable)
                {
                    result = default;
                    return false;
                }
                result = pendingResult;
                pendingResult = default;
                resultAvailable = false;
                return true;
            }
        }

        public void BeginShutdown()
        {
            lock (gate)
                shutdownStarted = true;
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [DllImport("ole32.dll")]
        private static extern int CoInitializeEx(
            IntPtr reserved,
            uint coInitialize);

        [DllImport("ole32.dll")]
        private static extern void CoUninitialize();

        private void RunDialog(IntPtr ownerWindow)
        {
            Debug.Log(
                "[DesktopMascotVrmFilePicker] STA picker thread started: " +
                "True");
            var result = Result(VrmFilePickerStatus.DialogFailed);
            var comInitialized = false;
            IFileOpenDialog dialog = null;
            IShellItem item = null;
            try
            {
                var initializeResult = CoInitializeEx(
                    IntPtr.Zero,
                    CoinitApartmentThreaded | CoinitDisableOle1Dde);
                Debug.Log(
                    "[DesktopMascotVrmFilePicker] COM initialize HRESULT: " +
                    $"0x{initializeResult:X8}");
                if (initializeResult < 0)
                {
                    result = Result(
                        VrmFilePickerStatus.ComInitializationFailed);
                    return;
                }
                comInitialized = true;

                var dialogType = Type.GetTypeFromCLSID(
                    FileOpenDialogClassId,
                    throwOnError: true);
                dialog = (IFileOpenDialog)Activator.CreateInstance(
                    dialogType);
                var filters = new[]
                {
                    new ComDialogFilterSpec
                    {
                        Name = "VRM 1.0 model (*.vrm)",
                        Specification = VrmFilterPattern
                    }
                };
                ThrowIfFailed(dialog.SetFileTypes(1, filters));
                ThrowIfFailed(dialog.GetOptions(out var options));
                options |=
                    FosNoChangeDirectory
                    | FosForceFileSystem
                    | FosPathMustExist
                    | FosFileMustExist
                    | FosDoNotAddToRecent;
                ThrowIfFailed(dialog.SetOptions(options));
                ThrowIfFailed(dialog.SetTitle("VRM 1.0ファイルを選択"));

                Debug.Log(
                    "[DesktopMascotVrmFilePicker] Dialog Show attempted: " +
                    "True");
                var showResult = dialog.Show(ownerWindow);
                Debug.Log(
                    "[DesktopMascotVrmFilePicker] Dialog Show HRESULT: " +
                    $"0x{showResult:X8}");
                if (showResult == CancelledHResult)
                {
                    result = Result(VrmFilePickerStatus.Cancelled);
                    return;
                }
                ThrowIfFailed(showResult);
                ThrowIfFailed(dialog.GetResult(out item));
                ThrowIfFailed(item.GetDisplayName(
                    SigDnFileSystemPath,
                    out var pathPointer));
                try
                {
                    var selectedPath =
                        Marshal.PtrToStringUni(pathPointer);
                    result = new VrmFilePickerResult(
                        string.IsNullOrEmpty(selectedPath)
                            ? VrmFilePickerStatus.DialogFailed
                            : VrmFilePickerStatus.Success,
                        selectedPath,
                        string.Empty);
                }
                finally
                {
                    Marshal.FreeCoTaskMem(pathPointer);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[DesktopMascotVrmFilePicker] Dialog exception type: " +
                    exception.GetType().Name);
                result = new VrmFilePickerResult(
                    VrmFilePickerStatus.DialogFailed,
                    string.Empty,
                    exception.GetType().Name);
            }
            finally
            {
                if (item != null && Marshal.IsComObject(item))
                    Marshal.FinalReleaseComObject(item);
                if (dialog != null && Marshal.IsComObject(dialog))
                    Marshal.FinalReleaseComObject(dialog);
                if (comInitialized)
                    CoUninitialize();
                Debug.Log(
                    "[DesktopMascotVrmFilePicker] Picker result/status: " +
                    $"True/{result.Status}");
                PublishResult(result);
            }
        }
#endif

        private void PublishResult(VrmFilePickerResult result)
        {
            lock (gate)
            {
                pendingResult = result;
                resultAvailable = true;
                running = false;
            }
        }

        private static VrmFilePickerResult Result(
            VrmFilePickerStatus status)
        {
            return new VrmFilePickerResult(
                status,
                string.Empty,
                string.Empty);
        }

        private static void ThrowIfFailed(int result)
        {
            if (result < 0)
                Marshal.ThrowExceptionForHR(result);
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct ComDialogFilterSpec
        {
            [MarshalAs(UnmanagedType.LPWStr)]
            internal string Name;
            [MarshalAs(UnmanagedType.LPWStr)]
            internal string Specification;
        }

        [ComImport]
        [Guid("42F85136-DB7E-439C-85F1-E4075D135FC8")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileOpenDialog
        {
            [PreserveSig]
            int Show(IntPtr owner);

            [PreserveSig]
            int SetFileTypes(
                uint count,
                [MarshalAs(
                    UnmanagedType.LPArray,
                    SizeParamIndex = 0)]
                ComDialogFilterSpec[] filters);
            [PreserveSig]
            int SetFileTypeIndex(uint index);
            [PreserveSig]
            int GetFileTypeIndex(out uint index);
            [PreserveSig]
            int Advise(IntPtr events, out uint cookie);
            [PreserveSig]
            int Unadvise(uint cookie);
            [PreserveSig]
            int SetOptions(uint options);
            [PreserveSig]
            int GetOptions(out uint options);
            [PreserveSig]
            int SetDefaultFolder(IShellItem folder);
            [PreserveSig]
            int SetFolder(IShellItem folder);
            [PreserveSig]
            int GetFolder(out IShellItem folder);
            [PreserveSig]
            int GetCurrentSelection(out IShellItem item);
            [PreserveSig]
            int SetFileName(
                [MarshalAs(UnmanagedType.LPWStr)] string name);
            [PreserveSig]
            int GetFileName(out IntPtr name);
            [PreserveSig]
            int SetTitle(
                [MarshalAs(UnmanagedType.LPWStr)] string title);
            [PreserveSig]
            int SetOkButtonLabel(
                [MarshalAs(UnmanagedType.LPWStr)] string label);
            [PreserveSig]
            int SetFileNameLabel(
                [MarshalAs(UnmanagedType.LPWStr)] string label);
            [PreserveSig]
            int GetResult(out IShellItem item);
            [PreserveSig]
            int AddPlace(IShellItem item, int placement);
            [PreserveSig]
            int SetDefaultExtension(
                [MarshalAs(UnmanagedType.LPWStr)] string extension);
            [PreserveSig]
            int Close(int result);
            [PreserveSig]
            int SetClientGuid(ref Guid guid);
            [PreserveSig]
            int ClearClientData();
            [PreserveSig]
            int SetFilter(IntPtr filter);
            [PreserveSig]
            int GetResults(out IntPtr items);
            [PreserveSig]
            int GetSelectedItems(out IntPtr items);
        }

        [ComImport]
        [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            [PreserveSig]
            int BindToHandler(
                IntPtr bindContext,
                ref Guid handlerId,
                ref Guid interfaceId,
                out IntPtr result);
            [PreserveSig]
            int GetParent(out IShellItem parent);
            [PreserveSig]
            int GetDisplayName(uint nameType, out IntPtr name);
            [PreserveSig]
            int GetAttributes(uint mask, out uint attributes);
            [PreserveSig]
            int Compare(
                IShellItem shellItem,
                uint hint,
                out int order);
        }
    }
}
