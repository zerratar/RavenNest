using System.Collections.Generic;

namespace Shinobytes.Ravenfall.Core.RuleEngine
{
    public class Gambit<TKnowledgeBase> : IGambit<TKnowledgeBase>
    {
        private readonly List<IGambitRule<TKnowledgeBase>> rules = new List<IGambitRule<TKnowledgeBase>>();
        private readonly object mutex = new object();
        private readonly GambitRuleGenerator ruleGenerator;

        public Gambit()
        {
            ruleGenerator = new GambitRuleGenerator();
        }

        public bool ProcessRules(TKnowledgeBase fact)
        {
            var anyRulesApplied = false;
            foreach (var rule in rules)
            {
                anyRulesApplied = anyRulesApplied || rule.Process(fact);
            }
            return anyRulesApplied;
        }

        public void AddRule(IGambitRule<TKnowledgeBase> rule)
        {
            lock (mutex) rules.Add(rule);
        }
        public void AddRules(IEnumerable<IGambitRule<TKnowledgeBase>> ruleCollection)
        {
            lock (mutex) rules.AddRange(ruleCollection);
        }

        public void RemoveRule(IGambitRule<TKnowledgeBase> rule)
        {
            lock (mutex) rules.Remove(rule);
        }

        public IGambitRuleAction<TKnowledgeBase> CreateAction(System.Action<TKnowledgeBase> onConditionMet)
        {
            return ruleGenerator.CreateAction<TKnowledgeBase>(onConditionMet);
        }

        public IGambitRuleCondition<TKnowledgeBase> CreateCondition(System.Func<TKnowledgeBase, bool> condition)
        {
            return ruleGenerator.CreateCondition<TKnowledgeBase>(condition);
        }

        public IGambitRule<TKnowledgeBase> CreateRule(string name, IGambitRuleCondition<TKnowledgeBase> condition, IGambitRuleAction<TKnowledgeBase> action)
        {
            return ruleGenerator.CreateRule(name, condition, action);
        }
    }
}
