using System;

namespace DesktopMascot.Runtime.CharacterPersistence
{
    internal readonly struct CharacterSelectionRecord :
        IEquatable<CharacterSelectionRecord>
    {
        internal CharacterSelectionRecord(
            int schemaVersion,
            int selectionVersion,
            string absolutePath,
            string contentSha256)
        {
            SchemaVersion = schemaVersion;
            SelectionVersion = selectionVersion;
            AbsolutePath = absolutePath ?? string.Empty;
            ContentSha256 = contentSha256 ?? string.Empty;
        }

        internal int SchemaVersion { get; }
        internal int SelectionVersion { get; }
        internal string AbsolutePath { get; }
        internal string ContentSha256 { get; }

        public bool Equals(CharacterSelectionRecord other) =>
            SchemaVersion == other.SchemaVersion
            && SelectionVersion == other.SelectionVersion
            && string.Equals(
                AbsolutePath,
                other.AbsolutePath,
                StringComparison.OrdinalIgnoreCase)
            && string.Equals(
                ContentSha256,
                other.ContentSha256,
                StringComparison.Ordinal);

        public override bool Equals(object value) =>
            value is CharacterSelectionRecord other && Equals(other);

        public override int GetHashCode() =>
            unchecked(
                ((SchemaVersion * 397) ^ SelectionVersion) * 397
                ^ StringComparer.OrdinalIgnoreCase
                    .GetHashCode(AbsolutePath)
                ^ StringComparer.Ordinal.GetHashCode(ContentSha256));
    }
}
