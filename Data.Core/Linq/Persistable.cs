using Data.Core.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using Common;

namespace Data.Core.Linq
{
    public static class Persistable
    {
        public static IEnumerable<U> Return<T, U>(this IPersistable<Path<T>> source, Expression<Func<PathReturner<T>, U>> selector) where T : IModel where U : IModel
        {
            var paths = ReturnPrivate<T, U>(source, selector).ToArray();

            if ((typeof(U).Implements<ILink>()))
            {
                return paths.SelectMany(p => p.Edges).OfType<U>();
            }
            else
            {
                return paths.SelectMany(p => p.Nodes).OfType<U>();
            }
        }

        private static IPersistable<Path<T>> ReturnPrivate<T, U>(this IPersistable<Path<T>> source, Expression<Func<PathReturner<T>, U>> selector) where T : IModel where U : IModel
        {
            if (source == null)
            {
                throw new InvalidOperationException("Source cannot be null");
            }
            if (selector == null)
            {
                throw new InvalidOperationException("Selector cannot be null");
            }
            var arguments = new Expression[2];
            arguments[0] = source.Expression;
            arguments[1] = Expression.Quote(selector);

            var paths = source.Provider.CreateQuery<Path<T>>(
                Expression.Call(null,
                    GetMethodInfo(new Func<IPersistable<Path<T>>, Expression<Func<PathReturner<T>, U>>, IPersistable<Path<T>>>(Persistable.ReturnPrivate<T, U>), source, selector),
                    arguments));
            return paths;
        }

        public static ModelList<T> Find<T>(this IDataSet<T> source, Expression<Func<T, bool>> filter) where T : IModel
        {
            return Find<T>(source, filterExpression: filter);
        }


        public static ModelList<T> Find<T>(this IDataSet<T> source,
            int offset = 0,
            int pageSize = 25,
            Expression<Func<T, bool>> filterExpression = null,
            SortField<T>[] sortExpressions = null) where T : IModel
        {
            if (source == null)
            {
                throw new InvalidOperationException("Source cannot be null");
            }

            var provider = source.Provider.CreateListProvider<T>();

            if (pageSize == -1)
            {
                pageSize = source.Repository.Policy.Limit;
            }
            
            var results = provider.Find(offset, pageSize, filterExpression, sortExpressions);
            if (source.Repository.Policy.TrackChanges)
            {
                var trackedResults = new ModelList<T>(
                    new TrackingEnumerator<T>(results, (ITrackingRepository)source.Repository).ToList(), 
                    results.Offset, 
                    results.TotalRecords, 
                    results.PageCount, 
                    results.PageSize);
                results = trackedResults;
            }
            return results;
        }

        private static MethodInfo GetMethodInfo<T1>(Func<T1> f) => f.Method;
        private static MethodInfo GetMethodInfo<T1, T2>(Func<T1, T2> f, T1 unused1) => f.Method;
        private static MethodInfo GetMethodInfo<T1, T2, T3>(Func<T1, T2, T3> f, T1 unused1, T2 unused2) => f.Method;
        private static MethodInfo GetMethodInfo<T1, T2, T3, T4>(Func<T1, T2, T3, T4> f, T1 unused1, T2 unused2, T3 unused3) => f.Method;
        private static MethodInfo GetMethodInfo<T1, T2, T3, T4, T5>(Func<T1, T2, T3, T4, T5> f, T1 unused1, T2 unused2, T3 unused3, T4 unused4) => f.Method;
        private static MethodInfo GetMethodInfo<T1, T2, T3, T4, T5, T6>(Func<T1, T2, T3, T4, T5, T6> f, T1 unused1, T2 unused2, T3 unused3, T4 unused4, T5 unused5) => f.Method;
    }


    public class PathSelector<M> where M : IModel
    {
        public PathSelector<T> Out<T>(Func<T, bool> predicate) where T : ILink
        {
            return new PathSelector<T>();
        }
        public PathSelector<U> Out<T, U>(Func<T, U, bool> predicate) where T : ILink where U : IModel
        {
            return new PathSelector<U>();
        }

        public PathSelector<T> In<T>(Func<T, bool> predicate) where T : ILink
        {
            return new PathSelector<T>();
        }
        public PathSelector<U> In<T, U>(Func<T, U, bool> predicate) where T : ILink where U : IModel
        {
            return new PathSelector<U>();
        }

        public M Root { get; }

        public static implicit operator bool(PathSelector<M> selector)
        {
            return true;
        }
    }

    public class PathReturner<U>
    {
        public ModelPathReturner<T> Edge<T>() where T : ILink
        {
            return new ModelPathReturner<T>();
        }

        public U Return() { return default(U); }
    } 

    public class ModelPathReturner<U> : PathReturner<U> where U : ILink
    {
        public PathReturner<T> Model<T>() where T : IModel
        {
            return new PathReturner<T>();
        }
    }
}
