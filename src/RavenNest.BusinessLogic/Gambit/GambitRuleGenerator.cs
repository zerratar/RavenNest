using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shinobytes.Ravenfall.Core.RuleEngine
{
    public class GambitRuleGenerator : IGambitRuleGenerator
    {
        public IGambitRuleAction<TKnowledgeBase> CreateAction<TKnowledgeBase>(Action<TKnowledgeBase> onConditionMet)
        {
            return new GambitRuleAction<TKnowledgeBase>(onConditionMet);
        }

        public IGambitRuleCondition<TKnowledgeBase> CreateCondition<TKnowledgeBase>(Func<TKnowledgeBase, bool> condition)
        {
            return new GambitRuleCondition<TKnowledgeBase>(condition);
        }

        public IGambitRule<TKnowledgeBase> CreateRule<TKnowledgeBase>(
            string name,
            IGambitRuleCondition<TKnowledgeBase> condition,
            IGambitRuleAction<TKnowledgeBase> action)
        {
            return new GambitRule<TKnowledgeBase>(name, condition, action);
        }

        private class GambitRuleCondition<TKnowledgeBase> : IGambitRuleCondition<TKnowledgeBase>
        {
            private Func<TKnowledgeBase, bool> condition;

            public GambitRuleCondition(Func<TKnowledgeBase, bool> condition)
            {
                this.condition = condition;
            }

            public bool TestCondition(TKnowledgeBase fact)
            {
                return condition(fact);
            }
        }

        private class GambitRuleAction<TKnowledgeBase> : IGambitRuleAction<TKnowledgeBase>
        {
            private Action<TKnowledgeBase> onConditionMet;

            public GambitRuleAction(Action<TKnowledgeBase> onConditionMet)
            {
                this.onConditionMet = onConditionMet;
            }

            public void Invoke(TKnowledgeBase fact)
            {
                onConditionMet.Invoke(fact);
            }
        }

        private class GambitRule<TKnowledgeBase> : IGambitRule<TKnowledgeBase>
        {
            private IGambitRuleCondition<TKnowledgeBase> condition;
            private IGambitRuleAction<TKnowledgeBase> action;

            public string Name { get; }

            public GambitRule(
                string name,
                IGambitRuleCondition<TKnowledgeBase> condition,
                IGambitRuleAction<TKnowledgeBase> action)
            {
                this.Name = name;
                this.condition = condition;
                this.action = action;
            }

            public bool Process(TKnowledgeBase fact)
            {
                if (!condition.TestCondition(fact))
                {
                    return false;
                }

                action.Invoke(fact);
                return true;
            }
        }
    }
}
