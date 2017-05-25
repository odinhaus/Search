using Data.Core.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Linq.Win
{
    public class ClientQueryPolice : QueryPolice
    {
        public ClientQueryPolice(QueryPolicy policy, QueryTranslator translator) : base(policy, translator) { }


        public override Expression BuildExecutionPlan(Expression query, Expression provider)
        {
            return ClientExecutionBuilder.Build(this.Translator.Linguist, this.Policy, query, provider);
        }
    }
}
