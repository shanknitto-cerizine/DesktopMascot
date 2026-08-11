namespace DesktopMascot.Runtime.Conversation.Domain
{
    internal sealed class ConversationEvent
    {
        private ConversationEvent(ConversationLogicalId triggerId)
        {
            TriggerId = triggerId;
        }

        internal ConversationLogicalId TriggerId { get; }

        internal static bool TryCreate(
            ConversationLogicalId triggerId,
            out ConversationEvent conversationEvent,
            out ConversationContractFailure failure)
        {
            conversationEvent = null;
            failure = ConversationContractFailure.None;
            if (!triggerId.IsValid)
            {
                failure = ConversationContractFailure.InvalidTriggerId;
                return false;
            }

            conversationEvent = new ConversationEvent(triggerId);
            return true;
        }
    }

    internal static class ConversationTriggerIds
    {
        internal const string MascotClick =
            "desktop-mascot.trigger.mascot-click";
        internal const string DragStart =
            "desktop-mascot.trigger.drag-start";
        internal const string DragEnd =
            "desktop-mascot.trigger.drag-end";
        internal const string Startup =
            "desktop-mascot.trigger.startup";
        internal const string Idle =
            "desktop-mascot.trigger.idle";
        internal const string TimeBased =
            "desktop-mascot.trigger.time-based";
        internal const string ExplicitRequest =
            "desktop-mascot.trigger.explicit-request";
    }
}
