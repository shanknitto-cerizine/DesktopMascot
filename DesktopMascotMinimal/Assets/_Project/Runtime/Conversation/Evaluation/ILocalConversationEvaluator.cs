using DesktopMascot.Runtime.Conversation.Domain;

namespace DesktopMascot.Runtime.Conversation.Evaluation
{
    // Local synchronous boundary for one bounded conversation evaluation.
    internal interface ILocalConversationEvaluator
    {
        ConversationEvaluationResult Evaluate(ConversationRequest request);
    }
}
