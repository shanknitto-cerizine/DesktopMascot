using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DesktopMascot.Runtime.CharacterPersistence
{
    internal enum CharacterSelectionValidationStatus
    {
        Valid = 0,
        InvalidJson = 1,
        MissingRequiredMember = 2,
        DuplicateMember = 3,
        UnknownMember = 4,
        TypeMismatch = 5,
        NegativeSchema = 6,
        UnsupportedSchema = 7,
        InvalidSelectionVersion = 8,
        UnsupportedSelectionVersion = 9,
        InvalidPath = 10,
        InvalidSha256 = 11
    }

    internal readonly struct CharacterSelectionValidationResult
    {
        internal CharacterSelectionValidationResult(
            CharacterSelectionValidationStatus status,
            CharacterSelectionRecord record)
        {
            Status = status;
            Record = record;
        }

        internal CharacterSelectionValidationStatus Status { get; }
        internal CharacterSelectionRecord Record { get; }
        internal bool IsValid =>
            Status == CharacterSelectionValidationStatus.Valid;
    }

    internal sealed class CharacterSelectionSerializer
    {
        internal const int CurrentSchemaVersion = 1;
        internal const int CurrentSelectionVersion = 1;
        internal const int MaximumPathCharacters = 32767;

        private const string SchemaName = "schemaVersion";
        private const string SelectionName = "selectionVersion";
        private const string PathName = "absolutePath";
        private const string ShaName = "contentSha256";

        private static readonly Regex KeyPattern = new Regex(
            "\"([A-Za-z][A-Za-z0-9]*)\"\\s*:",
            RegexOptions.CultureInvariant);
        private static readonly Regex SchemaPattern =
            IntegerPattern(SchemaName);
        private static readonly Regex SelectionPattern =
            IntegerPattern(SelectionName);
        private static readonly Regex PathPattern = StringPattern(PathName);
        private static readonly Regex ShaPattern = StringPattern(ShaName);
        private static readonly Regex ShaValuePattern = new Regex(
            "\\A[0-9A-Fa-f]{64}\\z",
            RegexOptions.CultureInvariant);
        private static readonly HashSet<string> RequiredNames =
            new HashSet<string>(StringComparer.Ordinal)
            {
                SchemaName,
                SelectionName,
                PathName,
                ShaName
            };

        [Serializable]
        private sealed class SerializedRecord
        {
            public int schemaVersion = int.MinValue;
            public int selectionVersion = int.MinValue;
            public string absolutePath;
            public string contentSha256;
        }

        internal CharacterSelectionValidationResult Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return Invalid(CharacterSelectionValidationStatus.InvalidJson);
            var trimmed = json.Trim();
            if (trimmed.Length < 2
                || trimmed[0] != '{'
                || trimmed[trimmed.Length - 1] != '}')
            {
                return Invalid(CharacterSelectionValidationStatus.InvalidJson);
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match match in KeyPattern.Matches(trimmed))
            {
                var name = match.Groups[1].Value;
                if (!RequiredNames.Contains(name))
                {
                    return Invalid(
                        CharacterSelectionValidationStatus.UnknownMember);
                }
                if (!seen.Add(name))
                {
                    return Invalid(
                        CharacterSelectionValidationStatus.DuplicateMember);
                }
            }
            foreach (var required in RequiredNames)
            {
                if (!seen.Contains(required))
                {
                    return Invalid(
                        CharacterSelectionValidationStatus
                            .MissingRequiredMember);
                }
            }
            if (seen.Count != RequiredNames.Count
                || !TryReadInteger(
                    SchemaPattern, trimmed, out var schema)
                || !TryReadInteger(
                    SelectionPattern,
                    trimmed,
                    out var selectionVersion)
                || PathPattern.Matches(trimmed).Count != 1
                || ShaPattern.Matches(trimmed).Count != 1)
            {
                return Invalid(
                    CharacterSelectionValidationStatus.TypeMismatch);
            }

            SerializedRecord parsed;
            try
            {
                parsed = JsonUtility.FromJson<SerializedRecord>(trimmed);
            }
            catch (ArgumentException)
            {
                return Invalid(CharacterSelectionValidationStatus.InvalidJson);
            }
            if (parsed == null
                || parsed.schemaVersion != schema
                || parsed.selectionVersion != selectionVersion
                || parsed.absolutePath == null
                || parsed.contentSha256 == null)
            {
                return Invalid(CharacterSelectionValidationStatus.InvalidJson);
            }
            if (schema < 0)
            {
                return Invalid(
                    CharacterSelectionValidationStatus.NegativeSchema);
            }
            if (schema != CurrentSchemaVersion)
            {
                return Invalid(
                    CharacterSelectionValidationStatus.UnsupportedSchema);
            }
            if (selectionVersion <= 0)
            {
                return Invalid(
                    CharacterSelectionValidationStatus
                        .InvalidSelectionVersion);
            }
            if (selectionVersion != CurrentSelectionVersion)
            {
                return Invalid(
                    CharacterSelectionValidationStatus
                        .UnsupportedSelectionVersion);
            }
            if (!TryNormalizePath(
                    parsed.absolutePath,
                    out var normalizedPath))
            {
                return Invalid(
                    CharacterSelectionValidationStatus.InvalidPath);
            }
            if (!TryNormalizeSha256(
                    parsed.contentSha256,
                    out var normalizedSha))
            {
                return Invalid(
                    CharacterSelectionValidationStatus.InvalidSha256);
            }
            return new CharacterSelectionValidationResult(
                CharacterSelectionValidationStatus.Valid,
                new CharacterSelectionRecord(
                    schema,
                    selectionVersion,
                    normalizedPath,
                    normalizedSha));
        }

        internal bool TryCreateRecord(
            string absolutePath,
            string contentSha256,
            out CharacterSelectionRecord record)
        {
            record = default;
            if (!TryNormalizePath(absolutePath, out var normalizedPath)
                || !TryNormalizeSha256(
                    contentSha256,
                    out var normalizedSha))
            {
                return false;
            }
            record = new CharacterSelectionRecord(
                CurrentSchemaVersion,
                CurrentSelectionVersion,
                normalizedPath,
                normalizedSha);
            return true;
        }

        internal bool TrySerialize(
            CharacterSelectionRecord record,
            out string json)
        {
            json = string.Empty;
            if (!TryCreateRecord(
                    record.AbsolutePath,
                    record.ContentSha256,
                    out var normalized)
                || record.SchemaVersion != CurrentSchemaVersion
                || record.SelectionVersion != CurrentSelectionVersion)
            {
                return false;
            }
            json = JsonUtility.ToJson(
                new SerializedRecord
                {
                    schemaVersion = normalized.SchemaVersion,
                    selectionVersion = normalized.SelectionVersion,
                    absolutePath = normalized.AbsolutePath,
                    contentSha256 = normalized.ContentSha256
                },
                true) + "\n";
            return true;
        }

        private static bool TryNormalizePath(
            string value,
            out string normalized)
        {
            normalized = string.Empty;
            if (string.IsNullOrWhiteSpace(value)
                || value.Length > MaximumPathCharacters
                || value.StartsWith(
                    "\\\\?\\",
                    StringComparison.Ordinal)
                || value.StartsWith(
                    "\\\\.\\",
                    StringComparison.Ordinal))
            {
                return false;
            }
            for (var index = 0; index < value.Length; ++index)
            {
                var character = value[index];
                if (char.IsControl(character))
                {
                    return false;
                }
                if (char.IsHighSurrogate(character))
                {
                    if (index + 1 >= value.Length
                        || !char.IsLowSurrogate(value[index + 1]))
                    {
                        return false;
                    }
                    ++index;
                }
                else if (char.IsLowSurrogate(character))
                {
                    return false;
                }
            }
            try
            {
                if (!Path.IsPathFullyQualified(value))
                    return false;
                normalized = Path.GetFullPath(value);
                return normalized.Length <= MaximumPathCharacters
                    && string.Equals(
                        Path.GetExtension(normalized),
                        ".vrm",
                        StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception exception)
                when (exception is ArgumentException
                      || exception is NotSupportedException
                      || exception is PathTooLongException)
            {
                return false;
            }
        }

        private static bool TryNormalizeSha256(
            string value,
            out string normalized)
        {
            normalized = string.Empty;
            if (value == null || !ShaValuePattern.IsMatch(value))
                return false;
            normalized = value.ToLowerInvariant();
            return true;
        }

        private static Regex IntegerPattern(string name) =>
            new Regex(
                "\"" + name +
                "\"\\s*:\\s*([+-]?\\d+)(?=\\s*[,}])",
                RegexOptions.CultureInvariant);

        private static Regex StringPattern(string name) =>
            new Regex(
                "\"" + name +
                "\"\\s*:\\s*\"(?:\\\\.|[^\"\\\\])*\"" +
                "(?=\\s*[,}])",
                RegexOptions.CultureInvariant);

        private static bool TryReadInteger(
            Regex pattern,
            string json,
            out int value)
        {
            value = 0;
            var matches = pattern.Matches(json);
            return matches.Count == 1
                && int.TryParse(
                    matches[0].Groups[1].Value,
                    NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture,
                    out value);
        }

        private static CharacterSelectionValidationResult Invalid(
            CharacterSelectionValidationStatus status) =>
            new CharacterSelectionValidationResult(status, default);
    }
}
