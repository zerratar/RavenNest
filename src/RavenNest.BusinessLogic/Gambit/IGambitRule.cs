namespace Shinobytes.Ravenfall.Core.RuleEngine
{
    public interface IGambitRule<TKnowledgeBase>
    {
        string Name { get; }
        bool Process(TKnowledgeBase fact);
    }
}
