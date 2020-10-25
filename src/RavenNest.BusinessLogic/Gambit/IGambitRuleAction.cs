namespace Shinobytes.Ravenfall.Core.RuleEngine
{
    public interface IGambitRuleAction<TKnowledgeBase>
    {
        void Invoke(TKnowledgeBase fact);
    }
}
