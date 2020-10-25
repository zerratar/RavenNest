namespace Shinobytes.Ravenfall.Core.RuleEngine
{
    public interface IGambitRuleCondition<TKnowledgeBase>
    {
        bool TestCondition(TKnowledgeBase fact);
    }
}
