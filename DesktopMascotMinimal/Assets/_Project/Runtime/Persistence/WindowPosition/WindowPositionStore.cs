using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DesktopMascot.Runtime
{
    internal sealed class WindowPositionStore
    {
        internal const int CurrentSchemaVersion = 1;
        internal const int MaximumAbsoluteCoordinate = 1000000;

        private static readonly Regex SchemaPattern = new Regex(
            "\"schemaVersion\"\\s*:\\s*([+-]?\\d+)",
            RegexOptions.CultureInvariant);
        private static readonly Regex XPattern = new Regex(
            "\"x\"\\s*:\\s*([+-]?\\d+)",
            RegexOptions.CultureInvariant);
        private static readonly Regex YPattern = new Regex(
            "\"y\"\\s*:\\s*([+-]?\\d+)",
            RegexOptions.CultureInvariant);

        private readonly string path;

        [Serializable]
        private sealed class SerializedRecord
        {
            public int schemaVersion = int.MinValue;
            public int x = int.MinValue;
            public int y = int.MinValue;
        }

        internal WindowPositionStore(string path)
        {
            this.path = path ??
                throw new ArgumentNullException(nameof(path));
        }

        internal string Path => path;

        internal WindowPositionLoadResult Load()
        {
            if (!File.Exists(path))
            {
                return Result(WindowPositionLoadStatus.Missing);
            }

            string json;
            try
            {
                json = File.ReadAllText(path, Encoding.UTF8);
            }
            catch (Exception exception)
            {
                return Result(
                    WindowPositionLoadStatus.ReadFailure,
                    exception.GetType().Name);
            }

            var trimmed = json.Trim();
            var schema = 0;
            var x = 0;
            var y = 0;
            var schemaRange = false;
            var xRange = false;
            var yRange = false;
            SerializedRecord parsedRecord = null;
            try
            {
                parsedRecord =
                    JsonUtility.FromJson<SerializedRecord>(trimmed);
            }
            catch (ArgumentException)
            {
                // The structural result below classifies this as a parse
                // failure without exposing parser details.
            }
            if (trimmed.Length < 2
                || trimmed[0] != '{'
                || trimmed[trimmed.Length - 1] != '}'
                || parsedRecord == null
                || !TryReadSingleInteger(
                    SchemaPattern, trimmed, out schema, out schemaRange)
                || !TryReadSingleInteger(
                    XPattern, trimmed, out x, out xRange)
                || !TryReadSingleInteger(
                    YPattern, trimmed, out y, out yRange))
            {
                var numericFailure =
                    schemaRange || xRange || yRange;
                return Result(
                    numericFailure
                        ? WindowPositionLoadStatus.NumericValidationFailure
                        : WindowPositionLoadStatus.ParseFailure);
            }
            if (parsedRecord.schemaVersion != schema
                || parsedRecord.x != x
                || parsedRecord.y != y)
            {
                return Result(WindowPositionLoadStatus.ParseFailure);
            }

            if (schema != CurrentSchemaVersion)
            {
                return Result(
                    WindowPositionLoadStatus.UnsupportedSchema,
                    schema.ToString(CultureInfo.InvariantCulture));
            }
            if (!IsCoordinateValid(x) || !IsCoordinateValid(y))
            {
                return Result(
                    WindowPositionLoadStatus.NumericValidationFailure);
            }
            return new WindowPositionLoadResult(
                WindowPositionLoadStatus.Loaded,
                new WindowPosition(x, y),
                string.Empty);
        }

        internal bool TrySave(
            WindowPosition position,
            out string error,
            Action<string, string> commitOverride = null)
        {
            error = string.Empty;
            if (!IsCoordinateValid(position.X)
                || !IsCoordinateValid(position.Y))
            {
                error = "numeric validation failure";
                return false;
            }

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
                var json = string.Format(
                    CultureInfo.InvariantCulture,
                    "{{\n  \"schemaVersion\": {0},\n  \"x\": {1},\n  \"y\": {2}\n}}\n",
                    CurrentSchemaVersion,
                    position.X,
                    position.Y);
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
                {
                    commitOverride(temporaryPath, path);
                }
                else
                {
                    Commit(temporaryPath, path);
                }
                return true;
            }
            catch (Exception exception)
            {
                error = exception.GetType().Name + ": " + exception.Message;
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
                    // A stale temporary file is preferable to damaging the
                    // previous valid final record.
                }
            }
        }

        private static void Commit(string temporaryPath, string finalPath)
        {
            if (File.Exists(finalPath))
                File.Replace(temporaryPath, finalPath, null);
            else
                File.Move(temporaryPath, finalPath);
        }

        private static bool TryReadSingleInteger(
            Regex pattern,
            string json,
            out int value,
            out bool rangeFailure)
        {
            value = 0;
            rangeFailure = false;
            var matches = pattern.Matches(json);
            if (matches.Count != 1)
                return false;
            if (int.TryParse(
                    matches[0].Groups[1].Value,
                    NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture,
                    out value))
            {
                return true;
            }
            rangeFailure = true;
            return false;
        }

        private static bool IsCoordinateValid(int value) =>
            value >= -MaximumAbsoluteCoordinate
            && value <= MaximumAbsoluteCoordinate;

        private static WindowPositionLoadResult Result(
            WindowPositionLoadStatus status,
            string detail = "") =>
            new WindowPositionLoadResult(
                status,
                default,
                detail);
    }
}
