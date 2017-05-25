using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Data.Core.Linq;
using System.Linq.Expressions;
using Common;
using Data.Core.Compilation;
using Data.Core.ComponentModel;
using System.Reflection;
using Common.Web;
using Common.Serialization.Binary;
using Common.Security;
using Microsoft.IdentityModel.Claims;
using Data.Core.Security;

namespace Data.Core.Linq.Win
{
    public class ClientRepository : ITrackingRepository
    {
        public ClientRepository()
        {
            this.IsValid = true;
            this.Policy = new ClientQueryPolicy();
            this.TrackingManager = new TrackingManager(this);
        }

        public virtual bool IsValid
        {
            get;
            private set;
        }
#if (DEBUG)
        public virtual IEnumerable<TrackedOperation> Operations
        {
            get
            {
                return _operations.AsEnumerable();
            }
        }
#endif

        public virtual IQueryPolicy Policy
        {
            get;
            protected set;
        }

        public virtual ICacheQueries QueryCache
        {
            get { return CacheProvider.Get<ClientRepository>(); }
        }

        public virtual TrackingManager TrackingManager { get; private set; }

        public virtual void AcceptChanges()
        {
            foreach(var tracked in TrackingManager)
            {
                tracked.Commit();
            }
        }

        /// <summary>
        /// Adds the identifiable instance to repository in the provided TrackingState, if possible.  The provided instance will be wrapped 
        /// in a runtime equivalent proxy, which should be used for all subsequent calls and state changes.  The original instance provided 
        /// will not be tracked or maintained by the state tracking system.  If you wish to create a new tracked instance of T, consider using the 
        /// Create<T>() method, which will construct and return a populated, tracked instance of T.  If Policy.TrackChanges is False, 
        /// the instance will still be proxied, but it will not be added to the Tracking Manager.  Changing Policy.TrackChanges to True, 
        /// and submitting the returned instance to Attach again will then add the item to the Tracking Manager.  The TrackingState submitted 
        /// is not guaranteed to be applied, depending on the current state of the Tracking Manager. Remarks for information on TrackingState.
        /// </summary>
        /// <typeparam name="T">the Type of identifiable to attach</typeparam>
        /// <param name="identifiable">the instance to attach</param>
        /// <param name="state">the desired TrackingState to attach with - note the provided state may be changed by the provider</param>
        /// <returns>an instance of the attached entity</returns>
        /// <remarks>In some cases, the provided state may conflict if the instance provided is already being tracked in an incompatible state.  E.g. 
        /// if the instance provided includes a populated Id property, and the state = TrackingState.Added, the state will be changed to Modified.</remarks>
        public virtual T Attach<T>(T model, TrackingState state = TrackingState.Unknown) where T : IModel
        {
            if (model is ISubModel)
            {
                // we dont track submodels
                return model;
            }
            else if (model is IPath)
            {
                var edges = new List<ILink>();
                foreach(var edge in ((IPath)model).Edges)
                {
                    var trackedEdge = (ILink)(IModel)Attach((IModel)edge);
                    trackedEdge.From = Attach(trackedEdge.From);
                    trackedEdge.To = Attach(trackedEdge.To);
                    edges.Add(trackedEdge);
                }
                var nodes = new List<IModel>();
                foreach (var node in ((IPath)model).Nodes)
                {
                    nodes.Add(Attach((IModel)node));
                }
                var tracked = Attach(((IPath)model).Root);

                ((IPath)model).Root = tracked;
                ((IPath)model).Nodes = nodes.ToArray();
                ((IPath)model).Edges = edges.ToArray();
                return model;
            }
            else if (model is ILink)
            {
                var tracked = TrackingManager.Get((IModel)model) as ILink;
                if (tracked == null)
                {
                    tracked = TrackingManager.Track<T>(model, state) as ILink;
                }
                else
                {
                    TrackingManager.SetState((ITrackedModel)tracked, state);
                }

                if (((ILink)model).To != null)
                {
                    var to = TrackingManager.Get(((ILink)model).To);
                    if (to == null)
                    {
                        to = TrackingManager.Track(((ILink)model).To, state) as ITrackedModel;
                    }
                    else
                    {
                        TrackingManager.SetState((ITrackedModel)to, state);
                    }
                    ((ILink)model).To = to;
                }

                if (((ILink)model).From != null)
                {
                    var from = TrackingManager.Get(((ILink)model).From);
                    if (from == null)
                    {
                        from = TrackingManager.Track(((ILink)model).From, state) as ITrackedModel;
                    }
                    else
                    {
                        TrackingManager.SetState((ITrackedModel)from, state);
                    }
                ((ILink)model).From = from;
                }

                return (T)tracked;
            }
            else if (model is IModel)
            {
                var tracked = TrackingManager.Get((IModel)model);
                if (tracked == null)
                {
                    tracked = TrackingManager.Track<T>(model, state);
                }
                else
                {
                    TrackingManager.SetState(tracked, state);
                }

                if (tracked == null) return model;
                else return (T)tracked;
            }
            else return model;
        }

        public virtual IModel Attach(IModel model, TrackingState state = TrackingState.Unknown)
        {
            if (model == null) return null;
            Type type = model.GetType().GetInterfaces().Single(t => t != typeof(IAny) && t.GetCustomAttribute<ModelAttribute>() != null);

            var call = Expression.Call(Expression.Constant(this),
                this.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance).Single(mi => mi.Name.Equals("Attach") && mi.IsGenericMethod).MakeGenericMethod(type),
                Expression.Parameter(type),
                Expression.Parameter(typeof(TrackingState)));
            var func = typeof(Func<,,>).MakeGenericType(type, typeof(TrackingState), type);
            var lambda = Expression.Lambda(func, call, call.Arguments.OfType<ParameterExpression>()).Compile();
            return (IModel)lambda.DynamicInvoke(model, state);
        }

        public virtual void ClearChanges()
        {
            foreach (var tracked in TrackingManager)
            {
                tracked.Revert();
            }
            TrackingManager.Clear();
        }

        public virtual T Create<T>(Func<T> initializer) where T : IModel
        {
            if (Policy.TrackChanges)
            {
                var entity = initializer();
                return Attach(entity, TrackingState.ShouldSave); // TrackingManager should clone this and return a proxy
            }
            else throw new InvalidOperationException("A new tracked instance cannot be created when Policy.TrackChanges equals False.");
        }

        public virtual T Create<T>(Action<T> memberInitializer = null) where T : IModel
        {
            if (Policy.TrackChanges)
            {
                var entity = RuntimeModelBuilder.CreateModelInstance<T>(ModelTypeConverter.ModelBaseType);
                if (memberInitializer != null)
                {
                    memberInitializer(entity);
                }
                return Attach(entity, TrackingState.ShouldSave); // TrackingManager should clone this and return a proxy
            }
            else throw new InvalidOperationException("A new tracked instance cannot be created when Policy.TrackChanges equals False.");
        }

        public virtual IModel Delete(string globalKey)
        {
            var model = new _Model(globalKey);
            return Delete(model);
        }

        public virtual IModel Delete(IModel model)
        {
            if (model == null) return model;

            var type = model.ModelType;

            var method = this.GetType().GetMethods()
                .Single(m => m.IsGenericMethod && m.Name.Equals("Delete") && 
                             m.GetParameters().Length == 1 && 
                            !m.GetParameters()[0].ParameterType.Equals(typeof(string)))
                .MakeGenericMethod(type);

            var p = Expression.Parameter(model.GetType(), "model");

            try
            {
                return (IModel)Expression.Lambda(
                            Expression.Call(Expression.Constant(this), method, p),
                        p).Compile().DynamicInvoke(model);
            }
            catch (TargetInvocationException tie)
            {
                throw tie.InnerException;
            }
        }

        public virtual T Delete<T>(T model) where T : IModel
        {
            if (model == null) return model;

            if (this.Policy.TrackChanges)
            {
                var tracked = TrackingManager.Track(model, TrackingState.ShouldDelete);
                tracked.IsDeleted = true;
                return (T)tracked;
            }
            else throw new InvalidOperationException("The repository cannot process changes when the Policy.TrackChanges setting is set to False.");
        }

        public virtual T Detach<T>(T model) where T : IModel
        {
            return TrackingManager.Remove<T>(model);
        }


        public virtual IModel Get(string globalId)
        {
            var idSplit = globalId.Split('/');

            var modelType = ModelTypeManager.GetModelType(idSplit[0]);
            var model = (IModel)RuntimeModelBuilder.CreateModelInstance(modelType, ModelTypeConverter.ModelBaseType);
            model.SetKey(idSplit[1]);

            var func = typeof(Func<,>).MakeGenericType(modelType, modelType);
            var method = this.GetType().GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .Single(m => m.Name.Equals("Get") && m.IsGenericMethod && m.GetParameters()[0].ParameterType != typeof(string));
            method = method.MakeGenericMethod(modelType);
            var identityParameter = Expression.Parameter(modelType);
            var del = Expression.Lambda(Expression.Call(Expression.Constant(this), method, identityParameter), identityParameter).Compile();
            return (IModel)del.DynamicInvoke(model);
        }

        public virtual IModel Get(IModel model)
        {
            var type = model.ModelType;
            var func = typeof(Func<,>).MakeGenericType(type, type);
            var method = this.GetType().GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .Single(m => m.Name.Equals("Get") && m.IsGenericMethod && m.GetParameters()[0].ParameterType != typeof(string));
            method = method.MakeGenericMethod(type);
            var identityParameter = Expression.Parameter(type);
            var del = Expression.Lambda(Expression.Call(Expression.Constant(this), method, identityParameter), identityParameter).Compile();
            return (IModel)del.DynamicInvoke(model);
        }

        public virtual T Get<T>(string key) where T : IModel
        {
            var model = RuntimeModelBuilder.CreateModelInstance<T>();
            model.SetKey(key);
            return (T)Get(model);
        }

        public virtual T Get<T>(T model) where T : IModel
        {
            var tracked = TrackingManager.Get(model);
            if (tracked == null || tracked.State == TrackingState.IsNotTracked)
            {
                return new DataSet<T>((QueryProvider)CreateProvider(), this, null).Find<T>(pageSize: 1, filterExpression: t => t.GetKey() == model.GetKey()).First();
            }
            else if (tracked.State == TrackingState.ShouldDelete && !Policy.ReturnTrackedDeletes)
            {
                throw new InvalidOperationException("The item has been submitted for deletion.");
            }
            else
            {
                return (T)tracked;
            }
        }

        public virtual IPersistableProvider CreateProvider()
        {
            return new ClientTrackingQueryProvider(this);
        }

        public virtual IEnumerator<ITrackedModel> GetEnumerator()
        {
            return TrackingManager.GetEnumerator();
        }

        public virtual TrackingState GetState<T>(T item) where T : IModel
        {
            return TrackingManager.Get(item).State;
        }

        public virtual ILinkSet<T> LinkSet<T>() where T : ILink
        {
            return new LinkSet<T>(new ClientTrackingQueryProvider(this), this, null);
        }

        public virtual IModelSet<T> ModelSet<T>() where T : IModel
        {
            return new ModelSet<T>(new ClientTrackingQueryProvider(this), this, null);
        }

        public virtual bool TryLock<T>(T item, out DateTime expires) where T : IModel
        {
            var api = AppContext.Current.Container.GetInstance<IApiClient>();
            var theLock = api.Call<LockedModel<T>>(new string[] { ModelTypeManager.GetModelName(item.ModelType), "Lock" },
                                                   new KeyValuePair<string, object>("item", item));
            expires = DateTime.MinValue;
            if (theLock == null) return false;

            expires = theLock.Lock.Expires;
            if (!theLock.Lock.IsExtension && item is ITrackedModel)
            {
                ((ITrackedModel)item).Commit(theLock.Model, true);
            }
            return true;
        }

        public void Unlock<T>(T item) where T : IModel
        {
            var api = AppContext.Current.Container.GetInstance<IApiClient>();
            api.Call<object>(new string[] { ModelTypeManager.GetModelName(item.ModelType), "Unlock" }, new KeyValuePair<string, object>("item", item));
        }

#if (DEBUG)
        protected List<TrackedOperation> _operations = new List<TrackedOperation>();
#endif
        public virtual ITrackingRepository SaveChanges()
        {
            // the general algorithm is as follows:
            // 0. update tracking states
            // 1. apply all deletes
            // 2. apply all inserts
            // 3. apply all updates
            //
            // The one complexity comes in step 2 when there exists co-dependent inserts in the change set
            // the simplest example would be given two types, A and B, where A has a property of type B
            // and B has a property of type A, and both are being inserted in the same change set.
            // 
            // To handle this situation, the insert process needs to detect and ignore dependent inserts, 
            // and allow them to be inserted discretely.  After insert, a new Id will be assigned, which should 
            // put the parent items which have already been inserted back into a ShouldSave state, and applied by step 3. 
            // For items not yet inserted, the dependent item that has been inserted won't be ignored, as it will now 
            // have an Id.
#if (DEBUG)
            _operations.Clear();
#endif
            //foreach (var instance in this.ToList())
            //{
            //    // capture any untracked items that may have been instantiated and attached to a tracked instance
            //    Cascade(instance, (item) => Attach(item));
            //}

            foreach (var changeGroup in CollectChangeGroups())
            {
                if (changeGroup.Key == TrackingState.ShouldDelete)
                {
                    ApplyDeletes(changeGroup);
                }
                else if (changeGroup.Key == TrackingState.ShouldSave)
                {
                    ApplySaves(changeGroup);
                }
            }

            this.AcceptChanges();

            return this;
        }

        protected virtual IEnumerable<IGrouping<TrackingState, ITrackedModel>> CollectChangeGroups()
        {
            TrackingManager.CalculateStates();

            var changes = TrackingManager
                            .Where(t => t.State != TrackingState.IsUnchanged && t.State != TrackingState.IsNotTracked)
                            .OrderBy(t => t is ILink ? 0 : 1)
                            .GroupBy(t => t.State);
            return changes;
        }

        protected virtual void ApplySaves(IGrouping<TrackingState, ITrackedModel> changeGroup)
        {
            var done = false;
            do
            {
                ApplyInserts(changeGroup.Where(ti => ti.IsNew)
                                     .OrderBy(ti => ti is ILink ? 1 : 0)
                                     .ToArray());
                ApplyUpdates(changeGroup.Where(ti => !ti.IsNew && ti.State == TrackingState.ShouldSave)
                                     .OrderBy(ti => ti is ILink ? 1 : 0)
                                     .ToArray());
                foreach(var tracked in changeGroup)
                {
                    tracked.CalculateState();
                }

                done = changeGroup.Count(ti => ti.IsNew) 
                     + changeGroup.Count(ti => !ti.IsNew && ti.State == TrackingState.ShouldSave) == 0;
            } while (!done);

        }

        protected virtual IEnumerable<ITrackedModel> TrackedModels
        {
            get
            {
                return TrackingManager;
            }
        }

        protected virtual IModelSet CreateModelSet(IModel model)
        {
            var type = model.ModelType;

            var thingSetType = typeof(ModelSet<>).MakeGenericType(type);
            return (IModelSet)Activator.CreateInstance(thingSetType, new object[] { new ClientTrackingQueryProvider(this), this, (Expression)null });
        }

        protected virtual void ApplyInserts(IEnumerable<ITrackedModel> inserts)
        {
            TrackingManager.IsTracking = false; // need to turn this off to prevent new items with new Ids being added
            foreach (var tracked in inserts)
            {
                if (tracked.IsDeleted)
                {
                    throw new InvalidOperationException("You cannot insert an item that has been deleted");
                }

                if (tracked is ILink)
                {
                    if (((ILink)tracked).From.IsDeleted || ((ILink)tracked).To.IsDeleted)
                    {
                        throw new InvalidOperationException("You cannot insert a Link that is between a deleted From or To model");
                    }
                }

                var model = Insert(CreateModelSet(tracked), tracked, ((IClaimsPrincipal)SecurityContext.Current.CurrentPrincipal).GetOrgUnits().First());
                tracked.Commit(model, true);
#if (DEBUG)
                _operations.Add(new TrackedOperation(tracked, OperationType.Insert));
#endif
            }
            TrackingManager.IsTracking = true;

        }

        protected virtual IModel Insert(IModelSet queryable, IModel model, IOrgUnit owner)
        {
            SetOwner(model);
            var identifiable = Expression.Convert(Expression.Constant(model), model.ModelType);
            var o = Expression.Constant(owner);

            var callMyself = Expression.Call(
                Expression.Constant(this),
                (MethodInfo)MethodInfo.GetCurrentMethod(),
                queryable.Expression,
                identifiable,
                o
            );
            return (IModel)new ClientTrackingQueryProvider(this).Execute(callMyself);
        }

        protected virtual void ApplyUpdates(IEnumerable<ITrackedModel> updates)
        {
            foreach (var tracked in updates)
            {
                if (tracked.IsDeleted)
                {
                    throw new InvalidOperationException("You cannot save an item that has been deleted");
                }

                if (tracked is ILink)
                {
                    if (((ILink)tracked).From.IsDeleted || ((ILink)tracked).To.IsDeleted)
                    {
                        throw new InvalidOperationException("You cannot save a Link that between a deleted From or To model");
                    }
                }

                var model = Update(CreateModelSet(tracked), tracked);
                tracked.Commit(model, true);
#if (DEBUG)
                _operations.Add(new TrackedOperation(tracked, OperationType.Update));
#endif
            }
        }

        protected virtual IModel Update(IModelSet queryable, IModel model)
        {
            SetOwner(model);
            var identifiable = model is IProxyModel
                ? (Expression)Expression.Convert(Expression.Constant(model), ((IProxyModel)model).ModelType)
                : Expression.Constant(model);

            var callMyself = Expression.Call(
                       Expression.Constant(this),
                       (MethodInfo)MethodInfo.GetCurrentMethod(),
                       queryable.Expression,
                       identifiable
                       );
            return (IModel)new ClientTrackingQueryProvider(this).Execute(callMyself);
        }

        protected virtual void ApplyDeletes(IGrouping<TrackingState, ITrackedModel> changeGroup)
        {
            foreach (var delete in changeGroup.ToArray())
            {
                var count = delete.IsNew ? 1 : this.DeleteInternal(delete);
#if (DEBUG)
                _operations.Add(new TrackedOperation(delete, OperationType.Delete));
#endif
                if (count == 1)
                {
                    TrackingManager.Remove(delete);
                    delete.IsDeleted = true;
                }
            }
        }

        protected virtual int DeleteInternal(string id)
        {
            return DeleteInternal((IModel)new _Model(id));
        }

        protected virtual int DeleteInternal(IModel model)
        {
            return DeleteInternal(CreateModelSet(model), model);
        }

        protected virtual int DeleteInternal<T>(T identity) where T : IModel
        {
            
            return DeleteInternal(CreateModelSet(identity), identity);
        }

        protected virtual int DeleteInternal(IModelSet queryable, IModel identity)
        {
            var genericDeleteInternal = this.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single(mi => mi.Name.Equals("Delete") && mi.IsGenericMethod && mi.GetParameters().Length == 2)
                .MakeGenericMethod(queryable.GetType().GetGenericArguments()[0]);
            return (int)genericDeleteInternal.Invoke(this, new object[] { queryable, identity});
        }

        protected virtual int Delete<T>(IModelSet<T> queryable, T identity) where T : IModel
        {
            SetOwner(identity);
            var identifiable = identity is IProxyModel
                ? (Expression)Expression.Convert(Expression.Constant(identity), ((IProxyModel)identity).ModelType)
                : Expression.Constant(identity);

            var callMyself = Expression.Call(
                Expression.Constant(this),
                ((MethodInfo)MethodInfo.GetCurrentMethod()).MakeGenericMethod(typeof(T)),
                Expression.Convert(queryable.Expression, typeof(IModelSet<T>)),
                identifiable
                );
            return (int)queryable.Provider.Execute(callMyself);
        }

        private void SetOwner<T>(T identity) where T : IModel
        {
            if (identity is _IOwnedModel && ((_IOwnedModel)identity).Owner == null)
            {
                ((_IOwnedModel)identity).Owner = ((IClaimsPrincipal)SecurityContext.Current.CurrentPrincipal).GetOrgUnits().First();
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public virtual ModelList<T> Query<T>(string query) where T:IModel
        {
            return new ClientTrackingQueryProvider(this).Query<T>(query);
        }

        public virtual ITrackingRepository CreateWorkContext()
        {
            return new ClientWorkRepository(this);
        }


#region IDisposable
        protected bool _disposed = false;

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                // release managed references here
                ClearChanges();
                this.TrackingManager = null;
                this.OnDisposeManaged();
            }

            // release unmanged references here
            this.OnDisposeUnmanaged();

            _disposed = true;
        }

        /// <summary>
        /// Closes the storage connection and disposes of all tracked items
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void OnDisposeManaged()
        {

        }

        protected virtual void OnDisposeUnmanaged()
        {

        }

        public int Delete<T>(string query) where T : IModel
        {
            throw new NotImplementedException();
        }

        public T Save<T>(string query) where T : IModel
        {
            throw new NotImplementedException();
        }



#endregion
    }
}
