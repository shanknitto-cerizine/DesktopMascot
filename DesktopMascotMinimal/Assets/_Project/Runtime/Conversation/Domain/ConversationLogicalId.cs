using System;

namespace DesktopMascot.Runtime.Conversation.Domain
{
    internal readonly struct ConversationLogicalId :
        IEquatable<ConversationLogicalId>
    {
        internal const int MaximumLength = 128;

        private readonly string value;

        private ConversationLogicalId(string value)
        {
            this.value = value;
        }

        internal string Value => value;
        internal bool IsValid => value != null;

        internal static bool TryCreate(
            string value,
            out ConversationLogicalId logicalId)
        {
            logicalId = default;
            if (!ConversationIdentifierValidation.IsValid(value))
                return false;

            logicalId = new ConversationLogicalId(value);
            return true;
        }

        public bool Equals(ConversationLogicalId other) =>
            string.Equals(value, other.value, StringComparison.Ordinal);

        public override bool Equals(object obj) =>
            obj is ConversationLogicalId other && Equals(other);

        public override int GetHashCode() =>
            value == null ? 0 : StringComparer.Ordinal.GetHashCode(value);

        public static bool operator ==(
            ConversationLogicalId left,
            ConversationLogicalId right) => left.Equals(right);

        public static bool operator !=(
            ConversationLogicalId left,
            ConversationLogicalId right) => !left.Equals(right);
    }

    internal readonly struct ConversationRequestId :
        IEquatable<ConversationRequestId>
    {
        private readonly string value;

        private ConversationRequestId(string value)
        {
            this.value = value;
        }

        internal string Value => value;
        internal bool IsValid => value != null;

        internal static bool TryCreate(
            string value,
            out ConversationRequestId requestId)
        {
            requestId = default;
            if (!ConversationIdentifierValidation.IsValid(value))
                return false;

            requestId = new ConversationRequestId(value);
            return true;
        }

        public bool Equals(ConversationRequestId other) =>
            string.Equals(value, other.value, StringComparison.Ordinal);

        public override bool Equals(object obj) =>
            obj is ConversationRequestId other && Equals(other);

        public override int GetHashCode() =>
            value == null ? 0 : StringComparer.Ordinal.GetHashCode(value);

        public static bool operator ==(
            ConversationRequestId left,
            ConversationRequestId right) => left.Equals(right);

        public static bool operator !=(
            ConversationRequestId left,
            ConversationRequestId right) => !left.Equals(right);
    }

    internal static class ConversationIdentifierValidation
    {
        internal static bool IsValid(string value)
        {
            if (string.IsNullOrEmpty(value)
                || value.Length > ConversationLogicalId.MaximumLength)
                return false;

            for (var index = 0; index < value.Length; ++index)
            {
                var character = value[index];
                if ((character < 'a' || character > 'z')
                    && (character < '0' || character > '9')
                    && character != '-'
                    && character != '.')
                    return false;

                if (character == '.'
                    && (index == 0
                        || index == value.Length - 1
                        || value[index - 1] == '.'))
                    return false;
            }

            return true;
        }
    }
}
