using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core
{
    public interface IPersistable : IEnumerable
    {
        //
        // Summary:
        //     Gets the type of the element(s) that are returned when the expression tree associated
        //     with this instance of System.Linq.IPersistable is executed.
        //
        // Returns:
        //     A System.Type that represents the type of the element(s) that are returned when
        //     the expression tree associated with this object is executed.
        Type ElementType { get; }
        //
        // Summary:
        //     Gets the expression tree that is associated with the instance of System.Linq.IPersistable.
        //
        // Returns:
        //     The System.Linq.Expressions.Expression that is associated with this instance
        //     of System.Linq.IPersistable.
        Expression Expression { get; }
        //
        // Summary:
        //     Gets the query provider that is associated with this data source.
        //
        // Returns:
        //     The System.Linq.IPersistableProvider that is associated with this data source.
        IPersistableProvider Provider { get; }
    }

    public interface IPersistable<T> : IPersistable, IEnumerable<T>
    {

    }
}
