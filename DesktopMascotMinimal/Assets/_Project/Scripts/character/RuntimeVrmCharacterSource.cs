using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UniGLTF;
using UniVRM10;
using UnityEngine;

namespace DesktopMascot.Character
{
    internal enum RuntimeVrmImportStatus
    {
        Success = 0,
        EmptyPath = 1,
        RelativePath = 2,
        FileNotFound = 3,
        DirectoryPath = 4,
        UnsupportedExtension = 5,
        EmptyFile = 6,
        FileTooLarge = 7,
        UnreadableFile = 8,
        HashFailed = 9,
        ImportFailed = 10,
        MissingRuntimeInstance = 11,
        Cancelled = 12,
        ShutdownStarted = 13,
        AlreadyStarted = 14
    }

    internal readonly struct RuntimeVrmPreflightResult
    {
        internal RuntimeVrmPreflightResult(
            RuntimeVrmImportStatus status,
            string fullPath,
            string stableSourceId,
            string contentSha256,
            string displayName,
            string detail)
        {
            Status = status;
            FullPath = fullPath;
            StableSourceId = stableSourceId;
            ContentSha256 = contentSha256;
            DisplayName = displayName;
            Detail = detail;
        }

        internal RuntimeVrmImportStatus Status { get; }
        internal string FullPath { get; }
        internal string StableSourceId { get; }
        internal string ContentSha256 { get; }
        internal string DisplayName { get; }
        internal string Detail { get; }
        internal bool Succeeded =>
            Status == RuntimeVrmImportStatus.Success
            && !string.IsNullOrEmpty(FullPath)
            && !string.IsNullOrEmpty(StableSourceId);
    }

    internal readonly struct RuntimeVrmImportResult
    {
        internal RuntimeVrmImportResult(
            RuntimeVrmImportStatus status,
            string stableSourceId,
            string displayName,
            Vrm10Instance vrmInstance,
            RuntimeGltfInstance runtimeInstance,
            string detail)
        {
            Status = status;
            StableSourceId = stableSourceId;
            DisplayName = displayName;
            VrmInstance = vrmInstance;
            RuntimeInstance = runtimeInstance;
            Detail = detail;
        }

        internal RuntimeVrmImportStatus Status { get; }
        internal string StableSourceId { get; }
        internal string DisplayName { get; }
        internal Vrm10Instance VrmInstance { get; }
        internal RuntimeGltfInstance RuntimeInstance { get; }
        internal string Detail { get; }
        internal bool Succeeded =>
            Status == RuntimeVrmImportStatus.Success
            && VrmInstance != null
            && RuntimeInstance != null;
    }

    internal sealed class RuntimeImportedCharacterHandle : IDisposable
    {
        private RuntimeGltfInstance runtimeInstance;
        private GameObject disposalRoot;
        private bool disposeRequested;

        internal RuntimeImportedCharacterHandle(
            RuntimeGltfInstance instance)
        {
            runtimeInstance = instance;
        }

        internal RuntimeGltfInstance RuntimeInstance => runtimeInstance;
        internal GameObject Root =>
            runtimeInstance != null
                ? runtimeInstance.Root
                : disposalRoot;
        internal bool IsDisposeRequested => disposeRequested;
        internal bool IsReleaseConfirmed =>
            disposeRequested && disposalRoot == null;
        internal bool IsReleased => IsReleaseConfirmed;

        internal bool TryRequestDispose(out string exceptionType)
        {
            exceptionType = string.Empty;
            if (disposeRequested)
                return true;

            var instance = runtimeInstance;
            if (instance == null)
            {
                disposeRequested = true;
                disposalRoot = null;
                return true;
            }

            var root = instance.Root;
            try
            {
                instance.Dispose();
                runtimeInstance = null;
                disposalRoot = root;
                disposeRequested = true;
                return true;
            }
            catch (Exception exception)
            {
                exceptionType = exception.GetType().Name;
                return false;
            }
        }

        public void Dispose()
        {
            if (!TryRequestDispose(out var exceptionType))
            {
                throw new InvalidOperationException(
                    $"Runtime character dispose failed: {exceptionType}");
            }
        }
    }

    internal sealed class RuntimeVrmCharacterSource
    {
        internal const long MaximumFileSizeBytes =
            256L * 1024L * 1024L;

        private const string Prefix =
            "[DesktopMascotRuntimeVrmImport]";

        private readonly CancellationTokenSource cancellation = new();
        private Task<RuntimeVrmPreflightResult> preflightTask;
        private Task<RuntimeVrmImportResult> importTask;
        private RuntimeImportedCharacterHandle preparedHandle;
        private bool operationStarted;
        private bool shutdownStarted;
        private bool ownershipTransferred;
        private bool cleanupCompleted;

        internal bool ImportStarted => operationStarted;
        internal bool ImportInProgress =>
            (preflightTask != null && !preflightTask.IsCompleted)
            || (importTask != null && !importTask.IsCompleted);
        internal Task<RuntimeVrmImportResult> ImportTask => importTask;
        internal bool ShutdownStarted => shutdownStarted;
        internal bool CleanupCompleted => cleanupCompleted;

        internal Task<RuntimeVrmImportResult> StartLoadAsync(string path)
        {
            var rejected = TryStartOperation();
            if (rejected.HasValue)
                return Task.FromResult(Result(rejected.Value, string.Empty));

            importTask = LoadPathCoreAsync(path, cancellation.Token);
            return importTask;
        }

        internal Task<RuntimeVrmPreflightResult>
            StartPreflightAsync(string path)
        {
            var rejected = TryStartOperation();
            if (rejected.HasValue)
            {
                return Task.FromResult(PreflightFailure(
                    rejected.Value,
                    string.Empty));
            }

            Log("Preflight started: True");
            preflightTask = PreflightCoreAsync(path, cancellation.Token);
            return preflightTask;
        }

        internal Task<RuntimeVrmImportResult> StartLoadAsync(
            RuntimeVrmPreflightResult preflight)
        {
            if (shutdownStarted)
            {
                return Task.FromResult(Result(
                    RuntimeVrmImportStatus.ShutdownStarted,
                    string.Empty));
            }
            if (!operationStarted
                || importTask != null
                || !preflight.Succeeded)
            {
                return Task.FromResult(Result(
                    RuntimeVrmImportStatus.AlreadyStarted,
                    string.Empty));
            }

            Log("Import started: True");
            importTask = LoadCoreAsync(preflight, cancellation.Token);
            return importTask;
        }

        internal RuntimeImportedCharacterHandle
            TransferPreparedOwnership()
        {
            if (shutdownStarted
                || ownershipTransferred
                || preparedHandle == null)
            {
                return null;
            }

            var handle = preparedHandle;
            preparedHandle = null;
            ownershipTransferred = true;
            Log("Ownership transferred to CharacterAssetManager: True");
            return handle;
        }

        internal void BeginShutdown()
        {
            if (shutdownStarted)
                return;
            shutdownStarted = true;
            Log("Cancellation requested: True");
            cancellation.Cancel();
        }

        internal bool Cleanup()
        {
            if (cleanupCompleted)
                return true;
            BeginShutdown();
            if (ImportInProgress)
                return false;

            preparedHandle?.Dispose();
            preparedHandle = null;
            cancellation.Dispose();
            cleanupCompleted = true;
            Log("Cleanup completed: True");
            return true;
        }

        internal static RuntimeVrmImportStatus
            ValidatePathStatusForDiagnostics(string path)
        {
            return ValidatePath(path).Status;
        }

        internal static RuntimeVrmImportStatus
            ClassifyFileLengthForDiagnostics(long length)
        {
            if (length == 0)
                return RuntimeVrmImportStatus.EmptyFile;
            if (length < 0 || length > MaximumFileSizeBytes)
                return RuntimeVrmImportStatus.FileTooLarge;
            return RuntimeVrmImportStatus.Success;
        }

        private async Task<RuntimeVrmImportResult> LoadPathCoreAsync(
            string inputPath,
            CancellationToken token)
        {
            var preflight = await PreflightCoreAsync(inputPath, token);
            if (!preflight.Succeeded)
                return Complete(Result(preflight.Status, preflight.Detail));
            Log("Import started: True");
            return await LoadCoreAsync(preflight, token);
        }

        private async Task<RuntimeVrmPreflightResult> PreflightCoreAsync(
            string inputPath,
            CancellationToken token)
        {
            try
            {
                var validation = await Task.Run(
                    () => ValidatePath(inputPath),
                    token);
                if (!validation.Succeeded)
                {
                    return CompletePreflight(PreflightFailure(
                        validation.Status,
                        validation.Detail));
                }

                token.ThrowIfCancellationRequested();

                string hash;
                try
                {
                    hash = await Task.Run(
                        () => ComputeSha256(validation.FullPath, token),
                        token);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    return CompletePreflight(new RuntimeVrmPreflightResult(
                        RuntimeVrmImportStatus.HashFailed,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        validation.DisplayName,
                        exception.GetType().Name));
                }

                token.ThrowIfCancellationRequested();
                var stableSourceId =
                    "desktop-mascot.runtime-vrm.sha256:" + hash;
                Log("SHA-256 calculated before import: True");
                return CompletePreflight(new RuntimeVrmPreflightResult(
                    RuntimeVrmImportStatus.Success,
                    validation.FullPath,
                    stableSourceId,
                    hash,
                    validation.DisplayName,
                    string.Empty));
            }
            catch (OperationCanceledException)
            {
                return CompletePreflight(PreflightFailure(
                    RuntimeVrmImportStatus.Cancelled,
                    nameof(OperationCanceledException)));
            }
            catch (Exception exception)
            {
                Log(
                    "Preflight exception type/status: " +
                    $"{exception.GetType().Name}/" +
                    RuntimeVrmImportStatus.HashFailed);
                return CompletePreflight(PreflightFailure(
                    RuntimeVrmImportStatus.HashFailed,
                    exception.GetType().Name));
            }
        }

        private async Task<RuntimeVrmImportResult> LoadCoreAsync(
            RuntimeVrmPreflightResult preflight,
            CancellationToken token)
        {
            try
            {
                var vrmInstance = await Vrm10.LoadPathAsync(
                    preflight.FullPath,
                    canLoadVrm0X: false,
                    controlRigGenerationOption:
                        ControlRigGenerationOption.Generate,
                    showMeshes: false,
                    ct: token);
                token.ThrowIfCancellationRequested();

                if (vrmInstance == null)
                {
                    return Complete(new RuntimeVrmImportResult(
                        RuntimeVrmImportStatus.ImportFailed,
                        preflight.StableSourceId,
                        preflight.DisplayName,
                        null,
                        null,
                        "LoadPathAsync returned null."));
                }

                var runtimeInstance =
                    vrmInstance.GetComponent<RuntimeGltfInstance>();
                if (runtimeInstance == null)
                {
                    UnityEngine.Object.Destroy(vrmInstance.gameObject);
                    return Complete(new RuntimeVrmImportResult(
                        RuntimeVrmImportStatus.MissingRuntimeInstance,
                        preflight.StableSourceId,
                        preflight.DisplayName,
                        null,
                        null,
                        nameof(RuntimeGltfInstance)));
                }

                vrmInstance.gameObject.SetActive(false);
                preparedHandle =
                    new RuntimeImportedCharacterHandle(runtimeInstance);
                return Complete(new RuntimeVrmImportResult(
                    RuntimeVrmImportStatus.Success,
                    preflight.StableSourceId,
                    preflight.DisplayName,
                    vrmInstance,
                    runtimeInstance,
                    string.Empty));
            }
            catch (OperationCanceledException)
            {
                preparedHandle?.Dispose();
                preparedHandle = null;
                return Complete(new RuntimeVrmImportResult(
                    RuntimeVrmImportStatus.Cancelled,
                    preflight.StableSourceId,
                    preflight.DisplayName,
                    null,
                    null,
                    nameof(OperationCanceledException)));
            }
            catch (Exception exception)
            {
                preparedHandle?.Dispose();
                preparedHandle = null;
                Debug.LogError(
                    $"{Prefix} Import exception type/status: " +
                    $"{exception.GetType().Name}/" +
                    RuntimeVrmImportStatus.ImportFailed);
                return Complete(new RuntimeVrmImportResult(
                    RuntimeVrmImportStatus.ImportFailed,
                    preflight.StableSourceId,
                    preflight.DisplayName,
                    null,
                    null,
                    exception.GetType().Name));
            }
        }

        private RuntimeVrmImportResult Complete(
            RuntimeVrmImportResult result)
        {
            Log($"Import completed with status: {result.Status}");
            return result;
        }

        private RuntimeVrmPreflightResult CompletePreflight(
            RuntimeVrmPreflightResult result)
        {
            Log($"Preflight completed with status: {result.Status}");
            return result;
        }

        private RuntimeVrmImportStatus? TryStartOperation()
        {
            if (shutdownStarted)
                return RuntimeVrmImportStatus.ShutdownStarted;
            if (operationStarted)
                return RuntimeVrmImportStatus.AlreadyStarted;
            operationStarted = true;
            return null;
        }

        private static PathValidationResult ValidatePath(string inputPath)
        {
            if (string.IsNullOrWhiteSpace(inputPath))
                return PathValidationResult.Failure(
                    RuntimeVrmImportStatus.EmptyPath,
                    "The path is empty.");

            var path = Unquote(inputPath.Trim());
            if (!Path.IsPathFullyQualified(path))
                return PathValidationResult.Failure(
                    RuntimeVrmImportStatus.RelativePath,
                    "An absolute path is required.");

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(path);
            }
            catch (Exception exception)
                when (exception is ArgumentException
                      || exception is NotSupportedException
                      || exception is PathTooLongException)
            {
                return PathValidationResult.Failure(
                    RuntimeVrmImportStatus.FileNotFound,
                    exception.GetType().Name);
            }

            if (Directory.Exists(fullPath))
                return PathValidationResult.Failure(
                    RuntimeVrmImportStatus.DirectoryPath,
                    "The path points to a directory.");
            if (!File.Exists(fullPath))
                return PathValidationResult.Failure(
                    RuntimeVrmImportStatus.FileNotFound,
                    "The file does not exist.");
            if (!string.Equals(
                    Path.GetExtension(fullPath),
                    ".vrm",
                    StringComparison.OrdinalIgnoreCase))
            {
                return PathValidationResult.Failure(
                    RuntimeVrmImportStatus.UnsupportedExtension,
                    "Only the .vrm extension is supported.");
            }

            try
            {
                var info = new FileInfo(fullPath);
                var lengthStatus =
                    ClassifyFileLengthForDiagnostics(info.Length);
                if (lengthStatus == RuntimeVrmImportStatus.EmptyFile)
                    return PathValidationResult.Failure(
                        RuntimeVrmImportStatus.EmptyFile,
                        "The file is empty.");
                if (lengthStatus == RuntimeVrmImportStatus.FileTooLarge)
                    return PathValidationResult.Failure(
                        RuntimeVrmImportStatus.FileTooLarge,
                        $"Maximum bytes: {MaximumFileSizeBytes}");
                using var stream = new FileStream(
                    fullPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read);
                return PathValidationResult.Success(
                    fullPath,
                    Path.GetFileName(fullPath));
            }
            catch (Exception exception)
                when (exception is IOException
                      || exception is UnauthorizedAccessException
                      || exception is System.Security.SecurityException)
            {
                return PathValidationResult.Failure(
                    RuntimeVrmImportStatus.UnreadableFile,
                    exception.GetType().Name);
            }
        }

        private static string ComputeSha256(
            string path,
            CancellationToken token)
        {
            using var sha = SHA256.Create();
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                1024 * 1024,
                FileOptions.SequentialScan);
            var buffer = new byte[1024 * 1024];
            while (true)
            {
                token.ThrowIfCancellationRequested();
                var count = stream.Read(buffer, 0, buffer.Length);
                if (count == 0)
                    break;
                sha.TransformBlock(buffer, 0, count, null, 0);
            }
            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            var builder = new StringBuilder(64);
            foreach (var value in sha.Hash)
                builder.Append(value.ToString("x2"));
            return builder.ToString();
        }

        private static string Unquote(string value)
        {
            if (value.Length >= 2
                && value[0] == '"'
                && value[value.Length - 1] == '"')
            {
                return value.Substring(1, value.Length - 2);
            }
            return value;
        }

        private static RuntimeVrmImportResult Result(
            RuntimeVrmImportStatus status,
            string detail)
        {
            return new RuntimeVrmImportResult(
                status,
                string.Empty,
                string.Empty,
                null,
                null,
                detail);
        }

        private static RuntimeVrmPreflightResult PreflightFailure(
            RuntimeVrmImportStatus status,
            string detail)
        {
            return new RuntimeVrmPreflightResult(
                status,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                detail);
        }

        private static void Log(string message)
        {
            Debug.Log($"{Prefix} {message}");
        }

        private readonly struct PathValidationResult
        {
            private PathValidationResult(
                RuntimeVrmImportStatus status,
                string fullPath,
                string displayName,
                string detail)
            {
                Status = status;
                FullPath = fullPath;
                DisplayName = displayName;
                Detail = detail;
            }

            internal RuntimeVrmImportStatus Status { get; }
            internal string FullPath { get; }
            internal string DisplayName { get; }
            internal string Detail { get; }
            internal bool Succeeded =>
                Status == RuntimeVrmImportStatus.Success;

            internal static PathValidationResult Success(
                string fullPath,
                string displayName)
            {
                return new PathValidationResult(
                    RuntimeVrmImportStatus.Success,
                    fullPath,
                    displayName,
                    string.Empty);
            }

            internal static PathValidationResult Failure(
                RuntimeVrmImportStatus status,
                string detail)
            {
                return new PathValidationResult(
                    status,
                    string.Empty,
                    string.Empty,
                    detail);
            }
        }
    }
}
