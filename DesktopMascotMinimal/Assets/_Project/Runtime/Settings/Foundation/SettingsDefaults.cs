using System;

namespace DesktopMascot.Runtime.Settings
{
    internal readonly struct SettingsValues :
        IEquatable<SettingsValues>
    {
        internal SettingsValues(
            int schemaVersion,
            int settingsVersion,
            bool firstRunCompleted)
        {
            SchemaVersion = schemaVersion;
            SettingsVersion = settingsVersion;
            FirstRunCompleted = firstRunCompleted;
        }

        internal int SchemaVersion { get; }
        internal int SettingsVersion { get; }
        internal bool FirstRunCompleted { get; }

        public bool Equals(SettingsValues other) =>
            SchemaVersion == other.SchemaVersion
            && SettingsVersion == other.SettingsVersion
            && FirstRunCompleted == other.FirstRunCompleted;

        public override bool Equals(object value) =>
            value is SettingsValues other && Equals(other);

        public override int GetHashCode() =>
            unchecked(
                ((SchemaVersion * 397) ^ SettingsVersion) * 397
                ^ FirstRunCompleted.GetHashCode());
    }

    internal static class SettingsDefaults
    {
        internal const int CurrentSchemaVersion = 1;
        internal const int CurrentSettingsVersion = 1;

        internal static SettingsValues Create() =>
            new SettingsValues(
                CurrentSchemaVersion,
                CurrentSettingsVersion,
                false);
    }
}
