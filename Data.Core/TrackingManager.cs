using Common;
using Data.Core.Compilation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Data.Core.Auditing;

namespace Data.Core
{
    public class TrackingManager : IEnumerable<ITrackedModel>
    {
        Dictionary<TrackedKey, ITrackedModel> _cached = new Dictionary<TrackedKey, ITrackedModel>(new TrackedKeyComparer());

        public TrackingManager(IRepository repository)
        {
            this.Repository = repository;
            this.IsTracking = repository.Policy.TrackChanges;
        }

        public IRepository Repository { get; private set; }

        public bool IsTracking { get; set; }

        public ITrackedModel Track(IModel instance, TrackingState state = TrackingState.Unknown)
        {
            var func = typeof(Func<,,>).MakeGenericType(instance.ModelType, typeof(TrackingState), typeof(ITrackedModel));
            var method = this.GetType().GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                                       .Single(mi => mi.Name.Equals("Track") && mi.IsGenericMethod)
                                       .MakeGenericMethod(instance.ModelType);

            var parms = new ParameterExpression[]
            {
                Expression.Parameter(instance.ModelType),
                Expression.Parameter(typeof(TrackingState))
            };

            var lambda = Expression.Lambda(func, Expression.Call(Expression.Constant(this), method, parms), parms).Compile();
            return (ITrackedModel)lambda.DynamicInvoke(instance, state);
        }

        /// <summary>
        /// Adds/updates an instance to the cache, and attempts to set the state, if possible.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="instance"></param>
        /// <param name="state"></param>
        /// <returns></returns>
        public ITrackedModel Track<T>(T instance, TrackingState state = TrackingState.Unknown) where T : IModel
        {
            if (!IsTracking || !Repository.Policy.TrackChanges) return null;

            ITrackedModel tracked = null;

            var key = new TrackedKey(instance is ITrackedModel ? instance as ITrackedModel : Create<T>(instance, state));
            bool exists = _cached.TryGetValue(key, out tracked);

            state = GetAdjustedState(tracked, instance as IModel, state);

            if (!exists && state == TrackingState.IsNotTracked)
            {
                return null;
            }
            else if (!exists)
            {
                tracked = (ITrackedModel)key.Model;
                _cached[key] = tracked;
            }
            else if (state != TrackingState.IsNotTracked)
            {
                // existing entry
                if (state != TrackingState.ShouldDelete)
                {
                    if (tracked.State == TrackingState.Unknown)
                    {
                        tracked.CalculateState();
                    }
                    if (tracked.State == TrackingState.IsUnchanged)
                    {
                        tracked.Commit((IModel)instance, true); // we have no local changes, so update the reference to use the new values
                    }
                }
                tracked.State = state;
            }
            else if (state == TrackingState.IsNotTracked)
            {
                if (exists)
                {
                    Remove<T>(instance);
                }
                return null;
            }
            return tracked;
        }

        public TrackingState SetState(ITrackedModel tracked, TrackingState state)
        {
            if (state == TrackingState.IsNotTracked)
            {
                Remove(tracked);
                return TrackingState.IsNotTracked;
            }
            else
            {
                tracked.State = GetAdjustedState(tracked, tracked, state);
            }
            return tracked.State;
        }


        
        private ITrackedModel Create<T>(T instance, TrackingState state) where T : IModel
        {
            ITrackedModel tracked;
            if (instance is ITrackedModel)
            {
                tracked = (ITrackedModel)instance;
            }
            else
            {
                if (typeof(T) == typeof(IAny))
                {
                    tracked = (ITrackedModel)RuntimeModelBuilder.CreateTrackedModelInstanceActivator(instance.ModelType)(instance, this.Repository);
                }
                else
                {
                    tracked = (ITrackedModel)RuntimeModelBuilder.CreateTrackedModelInstanceActivator<T>()(instance, this.Repository); // wrap the instance in a revertable model
                }
            }
            
            if (tracked.State != TrackingState.ShouldDelete)
            {
                tracked.State = state;
            }

            return tracked;
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////
        //              | Desired
        //===============================================================================================
        // Current      | New       | Modified  | Deleted           | Unknown   | Unchanged | NotTracked
        //-----------------------------------------------------------------------------------------------
        // Modified     |Modified   |Modified   |Deleted/NotTracked |Modified   |Unchanged  |NotTracked
        // Deleted      |Deleted    |Deleted    |Deleted            |Deleted    |Unchanged  |NotTracked
        // Unknown      |Unknown    |Modified   |Deleted            |Unknown    |Unchanged  |NotTracked
        // Unchanged    |Unchanged  |Modified   |Deleted            |Unknown    |Unchanged  |NotTracked
        // NotTracked   |NotTracked |Modified   |Deleted            |Unknown    |Unchanged  |NotTracked
        //===============================================================================================
        private TrackingState GetAdjustedState(ITrackedModel tracked, IModel instance, TrackingState desiredState)
        {
            if (desiredState == TrackingState.IsNotTracked)
                return TrackingState.IsNotTracked;

            if (tracked == null)
            {
                // this a new entry, just take whatever state is provided
                return desiredState;
            }
            if (desiredState == TrackingState.IsUnchanged)
            {
                return TrackingState.IsUnchanged;
            }
            else if (tracked.State == TrackingState.ShouldDelete && desiredState != TrackingState.IsNotTracked)
            {
                return TrackingState.ShouldDelete;
            }
            else if (tracked.State == TrackingState.ShouldSave
                 && (desiredState == TrackingState.ShouldSave || desiredState == TrackingState.Unknown))
            {
                return TrackingState.ShouldSave;
            }
            else if (tracked.State == TrackingState.ShouldSave && desiredState == TrackingState.ShouldDelete)
            {
                return string.IsNullOrEmpty(tracked.GetKey()) ? TrackingState.IsNotTracked : TrackingState.ShouldDelete;
            }
            else if (tracked.State == TrackingState.ShouldSave && desiredState == TrackingState.IsUnchanged)
            {
                return TrackingState.IsUnchanged;
            }
            else if (tracked.State == TrackingState.Unknown || tracked.State == TrackingState.IsUnchanged)
            {
                return desiredState;
            }
            else
                return desiredState;
        }

        /// <summary>
        /// Clears the tracking cache
        /// </summary>
        public void Clear()
        {
            _cached.Clear();
        }

        /// <summary>
        /// Removes the tracked instance from the cache
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="instance"></param>
        /// <returns></returns>
        public T Remove<T>(T instance)
        {
            var key = new TrackedKey(instance as ITrackedModel);
            _cached.Remove(key);
            return instance;
        }

        /// <summary>
        /// Returns true if the instance is being tracked
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="instance"></param>
        /// <returns></returns>
        public bool Contains<T>(T instance)
        {
            return _cached.ContainsKey(new TrackedKey(instance as ITrackedModel));
        }

        /// <summary>
        /// Gets a TrackedIdentifiable, if it exists, otherwise, returns null.
        /// </summary>
        /// <param name="instance"></param>
        /// <returns></returns>
        public ITrackedModel Get(IModel instance)
        {
            ITrackedModel item;
            if (instance is IModel
                && _cached.TryGetValue(new TrackedKey(instance), out item))
            {
                return item;
            }
            return null;
        }

        /// <summary>
        /// Computes all tracked item states
        /// </summary>
        public void CalculateStates()
        {
            foreach (var item in _cached.Values.ToArray())
                item.CalculateState(true);
        }

        /// <summary>
        /// Enumerates the current tracking cache
        /// </summary>
        /// <returns></returns>
        public IEnumerator<ITrackedModel> GetEnumerator()
        {
            foreach (var item in _cached.Values)
                yield return item;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        class TrackedKey : IEquatable<IModel>
        {
            public TrackedKey(IModel identifiable)
            {
                this.Model = identifiable;
            }

            public IModel Model { get; private set; }

            public bool Equals(IModel other)
            {
                var globalKey = this.Model?.GlobalKey() ?? null;
                var otherKey = other?.GlobalKey() ?? null;
                var trackedKey = (this.Model as ITrackedModel)?.TrackingKey ?? null;
                var otherTrackedKey = (other as ITrackedModel)?.TrackingKey ?? null;
                return this.Model != null
                    && other != null
                    && (this.Model == other
                    || (trackedKey != null && trackedKey.Equals(otherTrackedKey))
                    || (globalKey != null && globalKey.Equals(otherKey)));
            }

            public override bool Equals(object obj)
            {
                if (obj is TrackedKey)
                {
                    return Equals(((TrackedKey)obj).Model);
                }
                else return base.Equals(obj);
            }

            public override int GetHashCode()
            {
                return 0; // because our Ids can change after inserts, we need all our items to sit in the same bucket to force the dictionary to match them after their Ids change
            }
        }

        class TrackedKeyComparer : IEqualityComparer<TrackedKey>, IEqualityComparer
        {
            public bool Equals(TrackedKey x, TrackedKey y)
            {
                return (x == null && y == null)
                    || (
                    x != null && y != null
                    && ((TrackedKey)x).Equals((TrackedKey)y));
            }

            public new bool Equals(object x, object y)
            {
                return Equals(x as TrackedKey, y as TrackedKey);
            }

            public int GetHashCode(TrackedKey obj)
            {
                return obj.GetHashCode();
            }

            public int GetHashCode(object obj)
            {
                return obj.GetHashCode();
            }
        }
    }

    public static class IModelEx
    {
        public static string GlobalKey(this IModel model)
        {
            if (model == null || model.IsNew) return null;
            return string.Format("{0}/{1}", ModelTypeManager.GetModelName(model.ModelType), model.GetKey());
        }
    }


    internal class _Model : IModel
    {
        internal _Model(string globalKey)
        {
            var split = globalKey.Split('/');
            _key = split[1];
            ModelType = ModelTypeManager.GetModelType(split[0]);
        }
        public bool IsDeleted
        {
            get;
            set;
        }

        public Type ModelType
        {
            get;
            private set;
        }

        public bool IsNew
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public DateTime Created
        {
            get;

            set;
        }

        public DateTime Modified
        {
            get;

            set;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public event PropertyChangingEventHandler PropertyChanging;

        string _key = null;
        public string GetKey()
        {
            return _key;
        }

        public void SetKey(string value)
        {
            _key = value;
        }

        public IEnumerable<AuditedChange> Compare(IModel model, string prefix)
        {
            throw new NotSupportedException();
        }
    }
}
