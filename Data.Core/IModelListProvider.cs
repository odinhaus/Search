using Data.Core.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core
{
    public class SortField<T>
    {
        public SortField()
        {
            SortDirection = SortDirection.Asc;
        }

        public SortField(Expression<Func<T, object>> selector)
        {
            FieldSelector = selector;
        }

        public SortField(Expression<Func<T, object>> selector, SortDirection direction) : this(selector)
        {
            SortDirection = direction;
        }

        public Expression<Func<T, object>> FieldSelector { get; set; }
        public SortDirection SortDirection { get; set; }

        public static implicit operator Expression<Func<T, object>>(SortField<T> field)
        {
            return field.FieldSelector;
        }
        public static implicit operator SortField<T>(Expression<Func<T, object>> selector)
        {
            return new SortField<T>(selector);
        }

    }

    /// <summary>
    /// Provides a list of model type T items.  Implementers should enforce that T implements a form of IModel<> interface.  For the purposes of 
    /// keeping the interface clean, the generic type constraint do not impose this restriction, as it would add a redundant second generic 
    /// parameter into the method defintion, which woudl represent the IModel<> generic key type constraint.  
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IModelListProvider<T>
    {
        /// <summary>
        /// Gets a list of model elements of type T
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="pageSize"></param>
        /// <param name="filterExpression"></param>
        /// <param name="sortExpression"></param>
        /// <returns></returns>
        ModelList<T> Find(int offset = 0, int pageSize = 25, Expression<Func<T, bool>> filterExpression = null, SortField<T>[] sortExpressions = null);
        /// <summary>
        /// Gets a list of model elements of type T
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="pageSize"></param>
        /// <param name="filterExpression"></param>
        /// <param name="sortExpression"></param>
        /// <returns></returns>
        ModelList<T> Find(int offset = 0, int pageSize = 25, PredicateExpression filter = null, SortExpression[] sort = null);
    }
}
