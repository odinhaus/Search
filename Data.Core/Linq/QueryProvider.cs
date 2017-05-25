using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Linq
{
    public abstract class QueryProvider : IPersistableProvider, IProvideQueryText
    {
        public IPersistable<TIdentity> CreateQuery<TIdentity>(Expression expression)
        {
            return (IPersistable<TIdentity>)CreateQuery(expression);
        }

        public TResult Execute<TResult>(Expression expression)
        {
            return (TResult)Execute(expression);
        }

        public abstract IModelListProvider<TElement> CreateListProvider<TElement>();

        public abstract IPersistable CreateQuery(Expression expression);
        public abstract object Execute(Expression expression);
        public abstract string ToString(Expression queryExpression);
    }
}
