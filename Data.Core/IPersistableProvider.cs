using System.Linq.Expressions;

namespace Data.Core
{
    public interface IPersistableProvider
    {
        //
        // Summary:
        //     Constructs an System.Linq.IPersistable object that can evaluate the query represented
        //     by a specified expression tree.
        //
        // Parameters:
        //   expression:
        //     An expression tree that represents a LINQ query.
        //
        // Returns:
        //     An System.Linq.IPersistable that can evaluate the query represented by the specified
        //     expression tree.
        IPersistable CreateQuery(Expression expression);
        //
        // Summary:
        //     Constructs an System.Linq.IPersistable`1 object that can evaluate the query represented
        //     by a specified expression tree.
        //
        // Parameters:
        //   expression:
        //     An expression tree that represents a LINQ query.
        //
        // Type parameters:
        //   TElement:
        //     The type of the elements of the System.Linq.IPersistable`1 that is returned.
        //
        // Returns:
        //     An System.Linq.IPersistable`1 that can evaluate the query represented by the specified
        //     expression tree.
        IPersistable<TElement> CreateQuery<TElement>(Expression expression);
        /// <summary>
        /// Creates a paged result of TElement based on the supplied expression tree
        /// </summary>
        /// <typeparam name="TElement"></typeparam>
        /// <param name="expression"></param>
        /// <returns></returns>
        IModelListProvider<TElement> CreateListProvider<TElement>();
        //
        // Summary:
        //     Executes the query represented by a specified expression tree.
        //
        // Parameters:
        //   expression:
        //     An expression tree that represents a LINQ query.
        //
        // Returns:
        //     The value that results from executing the specified query.
        object Execute(Expression expression);
        //
        // Summary:
        //     Executes the strongly-typed query represented by a specified expression tree.
        //
        // Parameters:
        //   expression:
        //     An expression tree that represents a LINQ query.
        //
        // Type parameters:
        //   TResult:
        //     The type of the value that results from executing the query.
        //
        // Returns:
        //     The value that results from executing the specified query.
        TResult Execute<TResult>(Expression expression);
    }
}