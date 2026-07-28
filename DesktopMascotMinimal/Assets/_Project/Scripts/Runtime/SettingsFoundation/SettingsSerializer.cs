using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DesktopMascot.Runtime.Settings
{
    internal enum SettingsValidationStatus
    {
        Valid,
        InvalidJson,
        MissingRequiredMember,
        DuplicateMember,
        UnknownMember,
        TypeMismatch,
        NegativeSchema,
        UnsupportedSchema,
        InvalidSettingsVersion,
        UnsupportedSettingsVersion
    }

    internal readonly struct SettingsValidationResult
    {
        internal SettingsValidationResult(
            SettingsValidationStatus status,
            SettingsValues values,
            string detail)
        {
            Status = status;
            Values = values;
            Detail = detail;
        }

        internal SettingsValidationStatus Status { get; }
        internal SettingsValues Values { get; }
        internal string Detail { get; }
        internal bool IsValid =>
            Status == SettingsValidationStatus.Valid;
    }

    internal sealed class SettingsSerializer
    {
        private const string SchemaName = "schemaVersion";
        private const string SettingsName = "settingsVersion";
        private const string FirstRunName = "firstRunCompleted";

        private static readonly Regex KeyPattern = new Regex(
            "\"([A-Za-z][A-Za-z0-9]*)\"\\s*:",
            RegexOptions.CultureInvariant);
        private static readonly Regex SchemaPattern = IntegerPattern(
            SchemaName);
        private static readonly Regex SettingsPattern = IntegerPattern(
            SettingsName);
        private static readonly Regex FirstRunPattern = new Regex(
            "\"" + FirstRunName +
            "\"\\s*:\\s*(true|false)(?=\\s*[,}])",
            RegexOptions.CultureInvariant);

        private static readonly HashSet<string> RequiredNames =
            new HashSet<string>(StringComparer.Ordinal)
            {
                SchemaName,
                SettingsName,
                FirstRunName
            };

        [Serializable]
        private sealed class SerializedSettings
        {
            public int schemaVersion = int.MinValue;
            public int settingsVersion = int.MinValue;
            public bool firstRunCompleted;
        }

        internal SettingsValidationResult Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return Invalid(SettingsValidationStatus.InvalidJson);

            var trimmed = json.Trim();
            if (trimmed.Length < 2
                || trimmed[0] != '{'
                || trimmed[trimmed.Length - 1] != '}')
            {
                return Invalid(SettingsValidationStatus.InvalidJson);
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match match in KeyPattern.Matches(trimmed))
            {
                var name = match.Groups[1].Value;
                if (!RequiredNames.Contains(name))
                {
                    return Invalid(
                        SettingsValidationStatus.UnknownMember,
                        name);
                }
                if (!seen.Add(name))
                {
                    return Invalid(
                        SettingsValidationStatus.DuplicateMember,
                        name);
                }
            }
            foreach (var required in RequiredNames)
            {
                if (!seen.Contains(required))
                {
                    return Invalid(
                        SettingsValidationStatus.MissingRequiredMember,
                        required);
                }
            }
            if (seen.Count != RequiredNames.Count)
                return Invalid(SettingsValidationStatus.InvalidJson);

            if (!TryReadInteger(
                    SchemaPattern, trimmed, out var schema)
                || !TryReadInteger(
                    SettingsPattern, trimmed, out var settingsVersion)
                || !TryReadBoolean(
                    FirstRunPattern, trimmed, out var firstRunCompleted))
            {
                return Invalid(SettingsValidationStatus.TypeMismatch);
            }

            SerializedSettings parsed;
            try
            {
                parsed = JsonUtility.FromJson<SerializedSettings>(trimmed);
            }
            catch (ArgumentException)
            {
                return Invalid(SettingsValidationStatus.InvalidJson);
            }
            if (parsed == null
                || parsed.schemaVersion != schema
                || parsed.settingsVersion != settingsVersion
                || parsed.firstRunCompleted != firstRunCompleted)
            {
                return Invalid(SettingsValidationStatus.InvalidJson);
            }

            if (schema < 0)
                return Invalid(SettingsValidationStatus.NegativeSchema);
            if (schema != SettingsDefaults.CurrentSchemaVersion)
            {
                return Invalid(
                    SettingsValidationStatus.UnsupportedSchema,
                    schema.ToString(CultureInfo.InvariantCulture));
            }
            if (settingsVersion <= 0)
            {
                return Invalid(
                    SettingsValidationStatus.InvalidSettingsVersion);
            }
            if (settingsVersion
                != SettingsDefaults.CurrentSettingsVersion)
            {
                return Invalid(
                    SettingsValidationStatus.UnsupportedSettingsVersion,
                    settingsVersion.ToString(
                        CultureInfo.InvariantCulture));
            }

            return new SettingsValidationResult(
                SettingsValidationStatus.Valid,
                new SettingsValues(
                    schema,
                    settingsVersion,
                    firstRunCompleted),
                string.Empty);
        }

        internal bool TrySerialize(
            SettingsValues values,
            out string json)
        {
            json = string.Empty;
            if (values.SchemaVersion
                    != SettingsDefaults.CurrentSchemaVersion
                || values.SettingsVersion
                    != SettingsDefaults.CurrentSettingsVersion)
            {
                return false;
            }
            json = string.Format(
                CultureInfo.InvariantCulture,
                "{{\n  \"schemaVersion\": {0},\n" +
                "  \"settingsVersion\": {1},\n" +
                "  \"firstRunCompleted\": {2}\n}}\n",
                values.SchemaVersion,
                values.SettingsVersion,
                values.FirstRunCompleted ? "true" : "false");
            return true;
        }

        private static Regex IntegerPattern(string name) =>
            new Regex(
                "\"" + name +
                "\"\\s*:\\s*([+-]?\\d+)(?=\\s*[,}])",
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

        private static bool TryReadBoolean(
            Regex pattern,
            string json,
            out bool value)
        {
            value = false;
            var matches = pattern.Matches(json);
            return matches.Count == 1
                && bool.TryParse(
                    matches[0].Groups[1].Value,
                    out value);
        }

        private static SettingsValidationResult Invalid(
            SettingsValidationStatus status,
            string detail = "") =>
            new SettingsValidationResult(status, default, detail);
    }
}
