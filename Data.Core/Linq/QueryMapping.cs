using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Linq
{
    public abstract class QueryMapping
    {
        public Type RepositoryType { get; protected set; }
        public abstract QueryMapper CreateMapper(QueryTranslator translator);
        /// <summary>
        /// Determines the storage class name (i.e. vertex or table name) based on the type of the entity alone
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public virtual string GetStorageClass(Type type)
        {
            return ((ModelAttribute)type.GetCustomAttributes(typeof(ModelAttribute), true).FirstOrDefault())?.ModelName ?? type.Name;
        }

        /// <summary>
        /// Determines whether a given expression can be executed locally. 
        /// (It contains no parts that should be translated to the target environment.)
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        public virtual bool CanEvaluateLocally(Expression expression)
        {
            // any operation on a query can't be done locally
            ConstantExpression cex = expression as ConstantExpression;
            if (cex != null)
            {
                IPersistable query = cex.Value as IPersistable;
                if (query != null && query.Provider.GetType().IsSubclassOf(typeof(QueryProvider)))
                    return false;
            }
            MethodCallExpression mc = expression as MethodCallExpression;
            if (mc != null &&
                (mc.Method.DeclaringType == typeof(Enumerable) ||
                 mc.Method.DeclaringType == typeof(Queryable) ||
                 mc.Method.DeclaringType == typeof(Persistable) ||
                 mc.Method.DeclaringType.Implements(typeof(IRepository))
                 ))
            {
                return false;
            }
            if (expression.NodeType == ExpressionType.Convert &&
                expression.Type == typeof(object))
                return true;
            return expression.NodeType != ExpressionType.Parameter &&
                   expression.NodeType != ExpressionType.Lambda;
        }
    }
}
