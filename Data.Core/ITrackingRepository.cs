using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core
{
    public enum TrackingState
    {
        /// <summary>
        /// Item has been marked for Modification (or Addition, depending on the existence of an Id)
        /// </summary>
        ShouldSave,
        /// <summary>
        /// Item has been marked for Delete
        /// </summary>
        ShouldDelete,
        /// <summary>
        /// Item state is being tracked but is not currently in a known state (default state)
        /// </summary>
        Unknown,
        /// <summary>
        /// Item state is in a known unchanged state
        /// </summary>
        IsUnchanged,
        /// <summary>
        /// Item is not currently being tracked
        /// </summary>
        IsNotTracked
    }

    public interface ITrackingRepository : IRepository, IEnumerable<ITrackedModel>
    {
        /// <summary>
        /// Instantiates a new tracked instance of T.  The TrackingState given the new instance will depend on a combination 
        /// of variables including whether the new instance specifies a memberInitializer setting the Id property of the new instance, 
        /// and whether a similarly identified instance is already being tracked in a conflicting state.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="memberInitializer">an optional Action<T> delegate to call to initialize any property/field members</param>
        /// <returns>a tracked instance of T</returns>
        T Create<T>(Action<T> memberInitializer = null) where T : IModel;

        /// <summary>
        /// Instantiates a new tracked instance of T.  The TrackingState given the new instance will depend on a combination 
        /// of variables including whether the new instance specifies a memberInitializer setting the Id property of the new instance, 
        /// and whether a similarly identified instance is already being tracked in a conflicting state.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="initializer">a Func<T> to invoke to create the new instance</T>/></param>
        /// <returns>a tracked instance of T</returns>
        T Create<T>(Func<T> initializer) where T : IModel;

        /// <summary>
        /// Adds the identifiable instance to repository in the provided TrackingState, if possible.  The provided instance will likely be wrapped 
        /// in a runtime equivalent proxy, which should be used for all subsequent calls and state changes.  The original instance provided 
        /// will not be tracked or maintained by the state tracking system.  If you wish to create a new tracked instance of T, consider using the 
        /// Create<T>() method, which will construct and return a populated, tracked instance of T. See Remarks for information on TrackingState.
        /// </summary>
        /// <typeparam name="T">the Type of identifiable to attach</typeparam>
        /// <param name="identifiable">the instance to attach</param>
        /// <param name="state">the desired TrackingState to attach with - note the provided state may be changed by the provider</param>
        /// <returns>an instance of the attached entity</returns>
        /// <remarks>In some cases, the provided state may conflict if the instance provided is already being tracked in an incompatible state.  E.g. 
        /// if the instance provided includes a populated Id property, and the state = TrackingState.Added, the state will be changed to Modified.</remarks>
        T Attach<T>(T identifiable, TrackingState state = TrackingState.IsUnchanged) where T : IModel;

        /// <summary>
        /// Adds the identifiable instance to repository in the provided TrackingState, if possible.  The provided instance will likely be wrapped 
        /// in a runtime equivalent proxy, which should be used for all subsequent calls and state changes.  The original instance provided 
        /// will not be tracked or maintained by the state tracking system.  If you wish to create a new tracked instance of T, consider using the 
        /// Create<T>() method, which will construct and return a populated, tracked instance of T. See Remarks for information on TrackingState.
        /// </summary>
        /// <param name="identifiable">the instance to attach</param>
        /// <param name="state">the desired TrackingState to attach with - note the provided state may be changed by the provider</param>
        /// <returns>an instance of the attached entity</returns>
        /// /// <remarks>In some cases, the provided state may conflict if the instance provided is already being tracked in an incompatible state.  E.g. 
        /// if the instance provided includes a populated Id property, and the state = TrackingState.Added, the state will be changed to Modified.</remarks>
        IModel Attach(IModel identifiable, TrackingState state = TrackingState.IsUnchanged);

        /// <summary>
        /// Removes the identifiable instance from repository change tracking system
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="identifiable"></param>
        /// <param name="cascade">true to walk the object graph looking for other dependent IIdentifiable instances to also detach</param>
        /// <returns></returns>
        T Detach<T>(T identifiable) where T : IModel;

        /// <summary>
        /// Gets the tracking state for a given item
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="item"></param>
        /// <returns>the state of the tracked item.  If the item is not being tracked, TrackingState.Untracked will be returned.</returns>
        TrackingState GetState<T>(T item) where T : IModel;


        /// <summary>
        /// Marks an item with the provided IIdentifiable value as Deleted, returning the entity to delete
        /// </summary>
        /// <param name="identity"></param>
        /// <param name="cascade"></param>
        /// <returns></returns>
        IModel Delete(IModel identity);

        /// <summary>
        /// Deletes an item with the provided IIdentifiable value, returning the entity to delete
        /// </summary>
        /// <param name="identity"></param>
        /// <param name="cascade"></param>
        /// <returns></returns>
        T Delete<T>(T identity) where T : IModel;

        /// <summary>
        /// Deletes an item with the provided id, returning the entity to delete
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        IModel Delete(string id);

        /// <summary>
        /// Walks the change graph associated with entities attached to the repository instance and applies all associated Insert, Update and Delete operations 
        /// to the underlying data store.
        /// </summary>
        ITrackingRepository SaveChanges();

        /// <summary>
        /// Removes all current tracking information from the repository.
        /// </summary>
        void ClearChanges();

        /// <summary>
        /// Marks all tracked items as Unchanged.
        /// </summary>
        void AcceptChanges();
        /// <summary>
        /// Creates a repository instance that only applies persistence operations to the items worked on within the returned ITrackingRepository.  
        /// Allows consumers to create partitioned units of work within a single repository instance, which allows entity states to be shared across 
        /// the repository, without being persisted globally for the repository;
        /// </summary>
        /// <returns></returns>
        ITrackingRepository CreateWorkContext();
        TrackingManager TrackingManager { get; }
        /// <summary>
        /// Locks a record for the current user, making it unavailable for modification by other users until it is Unlocked.  If the user already holds a lock for 
        /// this record, the lock is extended.  If a lock is already held by another user, this method returns false.
        /// </summary>
        /// <typeparam name="T">the item to lock</typeparam>
        /// <param name="expires">when the lock will expire, if not renewed</param>
        /// <param name="item"></param>
        /// <returns>true if the lock is acquired or extended, false if a lock was not acquired</returns>
        bool TryLock<T>(T item, out DateTime expires) where T : IModel;
        /// <summary>
        /// Unlocks a record, making it free for modification by other users.  This method always succeeds, whether the user holds a lock or not.  
        /// In the event that the user does not hold a lock for the item, nothing happens, otherwise the lock is immediately expired.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="item"></param>
        void Unlock<T>(T item) where T : IModel;


#if (DEBUG)
            /// <summary>
            /// Used in Debug scenarios to observe what operations, and in what order, were actually performed against the data store.  This member does not 
            /// exist in Release build configurations.
            /// </summary>
            IEnumerable<TrackedOperation> Operations { get; }
#endif
    }

#if (DEBUG)
    public enum OperationType
    {
        Insert,
        Update,
        Delete
    }
    public class TrackedOperation
    {
        public TrackedOperation(ITrackedModel tracked, OperationType type)
        {
            this.OpetrationType = type;
            this.TrackedModel = tracked;
        }

        public OperationType OpetrationType { get; private set; }
        public ITrackedModel TrackedModel { get; private set; }
    }
#endif
}
