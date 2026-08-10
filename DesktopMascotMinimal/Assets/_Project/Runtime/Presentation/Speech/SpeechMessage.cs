namespace DesktopMascot.Runtime.Presentation.Speech
{
    internal enum SpeechThemeId { Normal = 1 }
    internal enum SpeechClosePolicy { ManualOrTimeout = 1 }
    internal enum SpeechTransitionStyle { None = 0 }
    internal enum SpeechDecorationKind { None = 0 }

    internal readonly struct SpeakerData
    {
        internal SpeakerData(
            string speakerId,
            string displayName,
            SpeechDecorationKind decoration)
        {
            SpeakerId = speakerId;
            DisplayName = displayName;
            Decoration = decoration;
        }
        internal string SpeakerId { get; }
        internal string DisplayName { get; }
        internal SpeechDecorationKind Decoration { get; }
    }

    internal sealed class SpeechMessage
    {
        internal SpeechMessage(
            string messageId,
            SpeakerData speaker,
            string body,
            SpeechThemeId theme,
            SpeechClosePolicy closePolicy,
            SpeechTransitionStyle transition)
        {
            MessageId = messageId;
            Speaker = speaker;
            Body = body;
            Theme = theme;
            ClosePolicy = closePolicy;
            Transition = transition;
        }
        internal string MessageId { get; }
        internal SpeakerData Speaker { get; }
        internal string Body { get; }
        internal SpeechThemeId Theme { get; }
        internal SpeechClosePolicy ClosePolicy { get; }
        internal SpeechTransitionStyle Transition { get; }
        internal bool IsValid =>
            !string.IsNullOrWhiteSpace(MessageId)
            && !string.IsNullOrWhiteSpace(Speaker.DisplayName)
            && !string.IsNullOrWhiteSpace(Body);
    }
}
