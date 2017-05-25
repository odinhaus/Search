using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Linq.Win
{
    public class ClientWorkRepository : ClientRepository
    {
        public ClientWorkRepository(ITrackingRepository parent)
        {
            this.Parent = parent;
            this.WorkContextKey = Guid.NewGuid().ToString();
        }

        public override bool IsValid
        {
            get
            {
                return Parent.IsValid && !_disposed;
            }
        }
#if (DEBUG)
        public override IEnumerable<TrackedOperation> Operations
        {
            get
            {
                return Parent.Operations.Where(o => o.TrackedModel.WorkContextKeys.Contains(this.WorkContextKey));
            }
        }
#endif

        public ITrackingRepository Parent { get; private set; }

        public override TrackingManager TrackingManager
        {
            get
            {
                return Parent.TrackingManager;
            }
        }

        [ThreadStatic]
        static ClientWorkRepository _current;
        public static ClientWorkRepository Current
        {
            get { return _current; }
            private set
            {
                _current = value;
            }
        }

        public string WorkContextKey { get; private set; }

        public override void AcceptChanges()
        {
            foreach (var tracked in TrackedModels.ToArray())
            {
                tracked.Commit();
            }
        }

        public override T Attach<T>(T model, TrackingState state = TrackingState.IsUnchanged)
        {
            var tracked = Parent.Attach(model, state);
            if (tracked is ITrackedModel && !((ITrackedModel)tracked).WorkContextKeys.Contains(this.WorkContextKey))
            {
                ((ITrackedModel)tracked).WorkContextKeys.Add(this.WorkContextKey);
            }

            if (tracked is IPath)
            {
                if (((IPath)tracked).Root is ITrackedModel && !((ITrackedModel)((IPath)tracked).Root).WorkContextKeys.Contains(this.WorkContextKey))
                {
                    ((ITrackedModel)((IPath)tracked).Root).WorkContextKeys.Add(this.WorkContextKey);
                }

                foreach(var edge in ((IPath)tracked).Edges)
                {
                    if (edge is ITrackedModel && !((ITrackedModel)edge).WorkContextKeys.Contains(this.WorkContextKey))
                    {
                        ((ITrackedModel)edge).WorkContextKeys.Add(this.WorkContextKey);
                    }
                }

                foreach (var node in ((IPath)tracked).Nodes)
                {
                    if (node is ITrackedModel && !((ITrackedModel)node).WorkContextKeys.Contains(this.WorkContextKey))
                    {
                        ((ITrackedModel)node).WorkContextKeys.Add(this.WorkContextKey);
                    }
                }
            }
            return tracked;
        }

        public override void ClearChanges()
        {
            foreach (var tracked in TrackedModels.ToArray())
            {
                tracked.WorkContextKeys.Remove(this.WorkContextKey);
                if(tracked.CalculateState(true) != TrackingState.IsUnchanged)
                    tracked.Revert();
                if (tracked.WorkContextKeys.Count == 0)
                {
                    TrackingManager.Remove(tracked);
                }
            }
        }

        public override T Create<T>(Func<T> initializer)
        {
            var tracked = Parent.Create(initializer);
            if (tracked is ITrackedModel && !((ITrackedModel)tracked).WorkContextKeys.Contains(this.WorkContextKey))
            {
                ((ITrackedModel)tracked).WorkContextKeys.Add(this.WorkContextKey);
            }
            return tracked;
        }

        public override T Create<T>(Action<T> memberInitializer = null)
        {
            var tracked = Parent.Create(memberInitializer);
            if (tracked is ITrackedModel && !((ITrackedModel)tracked).WorkContextKeys.Contains(this.WorkContextKey))
            {
                ((ITrackedModel)tracked).WorkContextKeys.Add(this.WorkContextKey);
            }
            return tracked;
        }

        public override IPersistableProvider CreateProvider()
        {
            return new ClientTrackingQueryProvider(this);
        }

        public override ITrackingRepository CreateWorkContext()
        {
            return this;
        }

        public override IModel Delete(string id)
        {
            return Parent.Delete(id);
        }

        public override IModel Delete(IModel model)
        {
            return Parent.Delete(model);
        }

        public override T Delete<T>(T model)
        {
            return Parent.Delete(model);
        }

        public override T Detach<T>(T model) 
        {
            var tracked = Parent.Detach(model);
            if (tracked is ITrackedModel && !((ITrackedModel)tracked).WorkContextKeys.Contains(this.WorkContextKey))
            {
                ((ITrackedModel)tracked).WorkContextKeys.Remove(this.WorkContextKey);
            }
            return tracked;
        }


        public override IModel Get(string id)
        {
            ClientWorkRepository.Current = this;
            var tracked = Parent.Get(id);
            if (tracked is ITrackedModel && !(((ITrackedModel)tracked).WorkContextKeys.Contains(this.WorkContextKey)))
            {
                ((ITrackedModel)tracked).WorkContextKeys.Add(this.WorkContextKey);
            }
            return tracked;
        }

        public override IModel Get(IModel model)
        {
            ClientWorkRepository.Current = this;
            var tracked = Parent.Get(model);
            if (tracked is ITrackedModel && !(((ITrackedModel)tracked).WorkContextKeys.Contains(this.WorkContextKey)))
            {
                ((ITrackedModel)tracked).WorkContextKeys.Add(this.WorkContextKey);
            }
            return tracked;
        }

        public override T Get<T>(string id) 
        {
            ClientWorkRepository.Current = this;
            var tracked = Parent.Get<T>(id);
            if (tracked is ITrackedModel && !(((ITrackedModel)tracked).WorkContextKeys.Contains(this.WorkContextKey)))
            {
                ((ITrackedModel)tracked).WorkContextKeys.Add(this.WorkContextKey);
            }
            return tracked;
        }

        public override T Get<T>(T model)
        {
            ClientWorkRepository.Current = this;
            var tracked = Parent.Get(model);
            if (tracked is ITrackedModel && !(((ITrackedModel)tracked).WorkContextKeys.Contains(this.WorkContextKey)))
            {
                ((ITrackedModel)tracked).WorkContextKeys.Add(this.WorkContextKey);
            }
            return tracked;
        }

        public override IEnumerator<ITrackedModel> GetEnumerator()
        {
            return TrackedModels.GetEnumerator();
        }


        public override TrackingState GetState<T>(T model)
        {
            if (model is ITrackedModel && ((ITrackedModel)model).WorkContextKeys.Contains(this.WorkContextKey))
            {
                return ((ITrackedModel)model).State;
            }
            else
            {
                return TrackingState.IsNotTracked;
            }
        }

        public override ILinkSet<T> LinkSet<T>()
        {
            ClientWorkRepository.Current = this;
            return new LinkSet<T>(new ClientTrackingQueryProvider(this), this, null);
        }

        public override IModelSet<T> ModelSet<T>()
        {
            ClientWorkRepository.Current = this;
            return new ModelSet<T>(new ClientTrackingQueryProvider(this), this, null);
        }

        public override ModelList<T> Query<T>(string query)
        {
            ClientWorkRepository.Current = this;
            return new ClientTrackingQueryProvider(this).Query<T>(query);
        }

        protected override IEnumerable<IGrouping<TrackingState, ITrackedModel>> CollectChangeGroups()
        {
            foreach (var tracked in TrackedModels)
            {
                tracked.CalculateState(true);
            }

            var changes = TrackedModels.Where(t => t.State != TrackingState.IsUnchanged && t.State != TrackingState.IsNotTracked)
                                       .OrderBy(t => t is ILink ? 1 : 0)
                                       .GroupBy(t => t.State);
            return changes;
        }

        protected override IEnumerable<ITrackedModel> TrackedModels
        {
            get
            {
                return TrackingManager.Where(t => t.WorkContextKeys.Any(k => k.Equals(this.WorkContextKey)));
            }
        }

        public override ITrackingRepository SaveChanges()
        {
            ClientWorkRepository.Current = this;
            return base.SaveChanges();
        }

        #region IDisposable
        protected override void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                // release managed references here
                this.OnDisposeManaged();
            }

            // release unmanged references here
            this.OnDisposeUnmanaged();

            _disposed = true;
        }
        protected override void OnDisposeManaged()
        {
            ClearChanges();
        }

#endregion
    }
}
