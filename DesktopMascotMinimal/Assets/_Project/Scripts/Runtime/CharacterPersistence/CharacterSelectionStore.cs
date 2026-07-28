using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace DesktopMascot.Runtime.CharacterPersistence
{
    internal enum CharacterSelectionStoreStatus
    {
        Ready = 0,
        PersistenceUnavailable = 1
    }

    internal enum CharacterSelectionStoreReadStatus
    {
        Loaded = 0,
        Missing = 1,
        ReadFailure = 2,
        RecordTooLarge = 3,
        PersistenceUnavailable = 4
    }

    internal sealed class CharacterSelectionStore
    {
        internal const long MaximumRecordBytes = 256L * 1024L;

        private readonly string path;
        private readonly Action<string> createDirectoryOverride;
        private bool initialized;
        private bool available;

        internal CharacterSelectionStore(
            string path,
            Action<string> directoryCreateOverride = null)
        {
            this.path = path ??
                throw new ArgumentNullException(nameof(path));
            createDirectoryOverride = directoryCreateOverride;
        }

        internal string Path => path;
        internal bool IsAvailable => initialized && available;

        internal CharacterSelectionStoreStatus Initialize()
        {
            if (initialized)
            {
                return available
                    ? CharacterSelectionStoreStatus.Ready
                    : CharacterSelectionStoreStatus.PersistenceUnavailable;
            }
            initialized = true;
            var directory = System.IO.Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory))
                return CharacterSelectionStoreStatus.PersistenceUnavailable;
            try
            {
                if (createDirectoryOverride != null)
                    createDirectoryOverride(directory);
                else
                    Directory.CreateDirectory(directory);
                available = true;
                return CharacterSelectionStoreStatus.Ready;
            }
            catch
            {
                available = false;
                return CharacterSelectionStoreStatus.PersistenceUnavailable;
            }
        }

        internal CharacterSelectionStoreReadStatus Read(
            out string json,
            out string failureType)
        {
            json = string.Empty;
            failureType = string.Empty;
            if (!IsAvailable)
            {
                return CharacterSelectionStoreReadStatus
                    .PersistenceUnavailable;
            }
            if (!File.Exists(path))
                return CharacterSelectionStoreReadStatus.Missing;
            try
            {
                var info = new FileInfo(path);
                if (info.Length < 0
                    || info.Length > MaximumRecordBytes)
                {
                    return CharacterSelectionStoreReadStatus.RecordTooLarge;
                }
                using var stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read);
                using var reader = new StreamReader(
                    stream,
                    new UTF8Encoding(false, true),
                    true);
                json = reader.ReadToEnd();
                return CharacterSelectionStoreReadStatus.Loaded;
            }
            catch (Exception exception)
            {
                failureType = exception.GetType().Name;
                return CharacterSelectionStoreReadStatus.ReadFailure;
            }
        }

        internal bool TryWriteAtomically(
            string json,
            out string failureType,
            Action<string, string> commitOverride = null)
        {
            failureType = string.Empty;
            if (!IsAvailable)
            {
                failureType = nameof(
                    CharacterSelectionStoreStatus.PersistenceUnavailable);
                return false;
            }
            var temporaryPath = path + "." +
                Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture) +
                ".tmp";
            try
            {
                using (var stream = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None))
                using (var writer = new StreamWriter(
                    stream,
                    new UTF8Encoding(false)))
                {
                    writer.Write(json);
                    writer.Flush();
                    stream.Flush(true);
                }
                if (commitOverride != null)
                    commitOverride(temporaryPath, path);
                else if (File.Exists(path))
                    File.Replace(temporaryPath, path, null);
                else
                    File.Move(temporaryPath, path);
                return true;
            }
            catch (Exception exception)
            {
                failureType = exception.GetType().Name;
                return false;
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                }
                catch
                {
                    // Never damage the previous final record because a
                    // temporary file could not be removed.
                }
            }
        }

        internal bool TryClear(out string failureType)
        {
            failureType = string.Empty;
            if (!IsAvailable)
            {
                failureType = nameof(
                    CharacterSelectionStoreStatus.PersistenceUnavailable);
                return false;
            }
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
                return true;
            }
            catch (Exception exception)
            {
                failureType = exception.GetType().Name;
                return false;
            }
        }
    }
}
