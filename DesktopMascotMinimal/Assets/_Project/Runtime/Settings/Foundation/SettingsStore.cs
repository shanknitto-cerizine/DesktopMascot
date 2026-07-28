using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace DesktopMascot.Runtime.Settings
{
    internal enum SettingsStoreReadStatus
    {
        Loaded,
        Missing,
        ReadFailure
    }

    internal sealed class SettingsStore
    {
        private readonly string path;

        internal SettingsStore(string path)
        {
            this.path = path ??
                throw new ArgumentNullException(nameof(path));
        }

        internal string Path => path;

        internal SettingsStoreReadStatus Read(
            out string json,
            out string error)
        {
            json = string.Empty;
            error = string.Empty;
            if (!File.Exists(path))
                return SettingsStoreReadStatus.Missing;
            try
            {
                json = File.ReadAllText(path, Encoding.UTF8);
                return SettingsStoreReadStatus.Loaded;
            }
            catch (Exception exception)
            {
                error =
                    exception.GetType().Name + ": " + exception.Message;
                return SettingsStoreReadStatus.ReadFailure;
            }
        }

        internal bool TryWriteAtomically(
            string json,
            out string error,
            Action<string, string> commitOverride = null)
        {
            error = string.Empty;
            var directory = System.IO.Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory))
            {
                error = "storage directory unavailable";
                return false;
            }

            var temporaryPath = path + "." +
                Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture) +
                ".tmp";
            try
            {
                Directory.CreateDirectory(directory);
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
                error =
                    exception.GetType().Name + ": " + exception.Message;
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
                    // Preserve the final record even if temporary cleanup
                    // itself fails.
                }
            }
        }
    }
}
