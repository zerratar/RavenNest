namespace Shinobytes.Ravenfall.Core.RuleEngine
{
    public interface IGambitGenerator
    {
        IGambit<TKnowledgeBase> CreateEngine<TKnowledgeBase>();
    }
}