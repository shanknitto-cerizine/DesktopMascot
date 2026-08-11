namespace DesktopMascot.Runtime.Conversation.Domain
{
    internal sealed class ConversationRequest
    {
        private ConversationRequest(
            ConversationRequestId requestId,
            ConversationEvent conversationEvent,
            ConversationLogicalId characterId)
        {
            RequestId = requestId;
            Event = conversationEvent;
            CharacterId = characterId;
        }

        internal ConversationRequestId RequestId { get; }
        internal ConversationEvent Event { get; }
        internal ConversationLogicalId CharacterId { get; }

        internal static bool TryCreate(
            ConversationRequestId requestId,
            ConversationEvent conversationEvent,
            ConversationLogicalId characterId,
            out ConversationRequest request,
            out ConversationContractFailure failure)
        {
            request = null;
            failure = ConversationContractFailure.None;
            if (!requestId.IsValid)
            {
                failure = ConversationContractFailure.InvalidRequestId;
                return false;
            }

            if (conversationEvent == null)
            {
                failure = ConversationContractFailure.MissingRequiredValue;
                return false;
            }

            if (!characterId.IsValid)
            {
                failure = ConversationContractFailure.InvalidCharacterId;
                return false;
            }

            request = new ConversationRequest(
                requestId,
                conversationEvent,
                characterId);
            return true;
        }
    }
}
