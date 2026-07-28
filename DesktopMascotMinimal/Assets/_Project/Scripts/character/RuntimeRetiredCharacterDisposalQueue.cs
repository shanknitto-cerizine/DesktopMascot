using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DesktopMascot.Character
{
    internal sealed class RuntimeRetiredCharacterDisposalQueue
    {
        internal const int FenceTimeoutFrames = 300;
        internal const double FenceTimeoutSeconds = 5.0;
        internal const int QueueWarningThreshold = 256;

        private const string Prefix =
            "[DesktopMascotRetiredCharacterDisposal]";

        private enum EntryState
        {
            AwaitingFence = 0,
            WaitingForFence = 1,
            DisposeRequested = 2,
            ShutdownFallback = 3
        }

        private sealed class Entry
        {
            internal Entry(RuntimeImportedCharacterHandle handle)
            {
                Handle = handle;
            }

            internal RuntimeImportedCharacterHandle Handle { get; }
            internal EntryState State { get; set; }
            internal GraphicsFence Fence { get; set; }
            internal int FenceCreatedFrame { get; set; }
            internal double FenceCreatedAt { get; set; }
        }

        private readonly List<Entry> entries = new();
        private bool queueWarningLogged;

        internal int Count => entries.Count;
        internal int MaximumCount { get; private set; }
        internal int FenceCohortCount { get; private set; }
        internal int FencePassedCount { get; private set; }
        internal int FenceTimeoutCount { get; private set; }
        internal int UnsupportedFallbackCount { get; private set; }
        internal int SafetyValidationFailureCount { get; private set; }
        internal int DisposeRequestedCount { get; private set; }
        internal int DisposeRequestFailureCount { get; private set; }
        internal int ReleaseConfirmedCount { get; private set; }
        internal bool GraphicsFenceSupported =>
            SystemInfo.supportsGraphicsFence;

        internal void EnsureCapacityForOneMore()
        {
            if (entries.Capacity < entries.Count + 1)
                entries.Capacity = entries.Count + 1;
        }

        internal void Enqueue(RuntimeImportedCharacterHandle handle)
        {
            if (handle == null)
                throw new ArgumentNullException(nameof(handle));

            entries.Add(new Entry(handle));
            MaximumCount = Math.Max(MaximumCount, entries.Count);
            if (!queueWarningLogged
                && entries.Count >= QueueWarningThreshold)
            {
                queueWarningLogged = true;
                Debug.LogWarning(
                    $"{Prefix} Diagnostics warning: retired queue count " +
                    $"reached {entries.Count}.");
            }
        }

        internal bool Remove(RuntimeImportedCharacterHandle handle)
        {
            for (var index = entries.Count - 1; index >= 0; --index)
            {
                if (!ReferenceEquals(entries[index].Handle, handle))
                    continue;
                entries.RemoveAt(index);
                return true;
            }
            return false;
        }

        internal void PumpAfterEndOfFrame(
            RuntimeImportedCharacterHandle activeHandle,
            GameObject activeRoot)
        {
            ConfirmReleasedEntries();
            PollFenceCohorts(activeHandle, activeRoot);
            ScheduleFenceCohort();
        }

        internal bool DisposeAllAfterPipelineStop()
        {
            var succeeded = true;
            for (var index = entries.Count - 1; index >= 0; --index)
            {
                var entry = entries[index];
                if (entry.Handle == null
                    || entry.Handle.IsDisposeRequested)
                {
                    entries.RemoveAt(index);
                    continue;
                }

                if (entry.Handle.TryRequestDispose(
                        out var exceptionType))
                {
                    ++DisposeRequestedCount;
                    entries.RemoveAt(index);
                    continue;
                }

                succeeded = false;
                ++DisposeRequestFailureCount;
                Debug.LogWarning(
                    $"{Prefix} Shutdown dispose request failed; " +
                    $"exception type/status: {exceptionType}/" +
                    "DisposeFailed.");
            }
            return succeeded;
        }

        private void ConfirmReleasedEntries()
        {
            for (var index = 0; index < entries.Count;)
            {
                var entry = entries[index];
                if (entry.State != EntryState.DisposeRequested
                    || !entry.Handle.IsReleaseConfirmed)
                {
                    ++index;
                    continue;
                }

                entries.RemoveAt(index);
                ++ReleaseConfirmedCount;
                Debug.Log(
                    $"{Prefix} Release confirmed: True; " +
                    $"outstanding retired: {entries.Count}");
            }
        }

        private void PollFenceCohorts(
            RuntimeImportedCharacterHandle activeHandle,
            GameObject activeRoot)
        {
            for (var index = 0; index < entries.Count; ++index)
            {
                var entry = entries[index];
                if (entry.State != EntryState.WaitingForFence)
                    continue;

                bool passed;
                try
                {
                    passed = entry.Fence.passed;
                }
                catch (Exception exception)
                {
                    entry.State = EntryState.ShutdownFallback;
                    ++UnsupportedFallbackCount;
                    Debug.LogWarning(
                        $"{Prefix} Fence polling unavailable; " +
                        "using shutdown fallback. Exception type/status: " +
                        $"{exception.GetType().Name}/FencePollingFailed.");
                    continue;
                }

                if (passed)
                {
                    ++FencePassedCount;
                    RequestDisposeIfSafe(entry, activeHandle, activeRoot);
                    continue;
                }

                if (Time.frameCount - entry.FenceCreatedFrame
                        < FenceTimeoutFrames
                    && Time.realtimeSinceStartupAsDouble
                        - entry.FenceCreatedAt < FenceTimeoutSeconds)
                {
                    continue;
                }

                entry.State = EntryState.ShutdownFallback;
                ++FenceTimeoutCount;
                Debug.LogWarning(
                    $"{Prefix} GraphicsFence timeout; early dispose " +
                    "cancelled and shutdown fallback retained.");
            }
        }

        private void RequestDisposeIfSafe(
            Entry entry,
            RuntimeImportedCharacterHandle activeHandle,
            GameObject activeRoot)
        {
            var root = entry.Handle.Root;
            if (ReferenceEquals(entry.Handle, activeHandle)
                || (root != null
                    && (root.activeSelf
                        || ReferenceEquals(root, activeRoot))))
            {
                entry.State = EntryState.ShutdownFallback;
                ++SafetyValidationFailureCount;
                Debug.LogWarning(
                    $"{Prefix} Safety validation failed; early dispose " +
                    "cancelled and shutdown fallback retained.");
                return;
            }

            if (!entry.Handle.TryRequestDispose(out var exceptionType))
            {
                entry.State = EntryState.ShutdownFallback;
                ++DisposeRequestFailureCount;
                Debug.LogWarning(
                    $"{Prefix} Dispose request failed; no retry before " +
                    "shutdown. Exception type/status: " +
                    $"{exceptionType}/DisposeFailed.");
                return;
            }

            entry.State = EntryState.DisposeRequested;
            ++DisposeRequestedCount;
            Debug.Log(
                $"{Prefix} Dispose requested: True; release confirmed: " +
                "False.");
        }

        private void ScheduleFenceCohort()
        {
            var awaitingCount = 0;
            foreach (var entry in entries)
            {
                if (entry.State == EntryState.AwaitingFence)
                    ++awaitingCount;
            }
            if (awaitingCount == 0)
                return;

            if (!SystemInfo.supportsGraphicsFence)
            {
                foreach (var entry in entries)
                {
                    if (entry.State == EntryState.AwaitingFence)
                        entry.State = EntryState.ShutdownFallback;
                }
                UnsupportedFallbackCount += awaitingCount;
                Debug.Log(
                    $"{Prefix} GraphicsFence supported: False; " +
                    "shutdown disposal fallback selected.");
                return;
            }

            var commandBuffer = new CommandBuffer
            {
                name = "DesktopMascot Retired Character Disposal Fence"
            };
            try
            {
                var fence = commandBuffer.CreateGraphicsFence(
                    GraphicsFenceType.AsyncQueueSynchronisation,
                    SynchronisationStageFlags.PixelProcessing);
                Graphics.ExecuteCommandBuffer(commandBuffer);
                var frame = Time.frameCount;
                var createdAt = Time.realtimeSinceStartupAsDouble;
                foreach (var entry in entries)
                {
                    if (entry.State != EntryState.AwaitingFence)
                        continue;
                    entry.Fence = fence;
                    entry.FenceCreatedFrame = frame;
                    entry.FenceCreatedAt = createdAt;
                    entry.State = EntryState.WaitingForFence;
                }
                ++FenceCohortCount;
                Debug.Log(
                    $"{Prefix} GraphicsFence cohort scheduled: " +
                    $"{awaitingCount}.");
            }
            catch (Exception exception)
            {
                foreach (var entry in entries)
                {
                    if (entry.State == EntryState.AwaitingFence)
                        entry.State = EntryState.ShutdownFallback;
                }
                UnsupportedFallbackCount += awaitingCount;
                Debug.LogWarning(
                    $"{Prefix} GraphicsFence unavailable; shutdown " +
                    "disposal fallback selected. Exception type/status: " +
                    $"{exception.GetType().Name}/FenceCreationFailed.");
            }
            finally
            {
                commandBuffer.Release();
            }
        }
    }
}
