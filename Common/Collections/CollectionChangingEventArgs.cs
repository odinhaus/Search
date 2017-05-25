using System.Collections;
using System.Collections.Specialized;

namespace Common.Collections
{
    public class CollectionChangingEventArgs
    {
        //
        // Summary:
        //     Initializes a new instance of the System.Collections.Specialized.NotifyCollectionChangedEventArgs
        //     class that describes a multi-item System.Collections.Specialized.NotifyCollectionChangedAction.Replace
        //     change.
        //
        // Parameters:
        //   action:
        //     The action that caused the event. This can only be set to System.Collections.Specialized.NotifyCollectionChangedAction.Replace.
        //
        //   newItems:
        //     The new items that are replacing the original items.
        //
        //   oldItems:
        //     The original items that are replaced.
        //
        //   startingIndex:
        //     The index of the first item of the items that are being replaced.
        //
        // Exceptions:
        //   T:System.ArgumentException:
        //     If action is not Replace.
        //
        //   T:System.ArgumentNullException:
        //     If oldItems or newItems is null.
        public CollectionChangingEventArgs(NotifyCollectionChangedAction action, IList newItems, IList oldItems, int startingIndex, int oldIndex)
        {
            Action = action;
            NewItems = newItems;
            NewStartingIndex = startingIndex;
            OldItems = oldItems;
            OldStartingIndex = oldIndex;
        }


        //
        // Summary:
        //     Gets the action that caused the event.
        //
        // Returns:
        //     A System.Collections.Specialized.NotifyCollectionChangedAction value that describes
        //     the action that caused the event.
        public NotifyCollectionChangedAction Action { get; private set; }
        //
        // Summary:
        //     Gets the list of new items involved in the change.
        //
        // Returns:
        //     The list of new items involved in the change.
        public IList NewItems { get; private set; }
        //
        // Summary:
        //     Gets the index at which the change occurred.
        //
        // Returns:
        //     The zero-based index at which the change occurred.
        public int NewStartingIndex { get; private set; }
        //
        // Summary:
        //     Gets the list of items affected by a System.Collections.Specialized.NotifyCollectionChangedAction.Replace,
        //     Remove, or Move action.
        //
        // Returns:
        //     The list of items affected by a System.Collections.Specialized.NotifyCollectionChangedAction.Replace,
        //     Remove, or Move action.
        public IList OldItems { get; private set; }
        //
        // Summary:
        //     Gets the index at which a System.Collections.Specialized.NotifyCollectionChangedAction.Move,
        //     Remove, or Replace action occurred.
        //
        // Returns:
        //     The zero-based index at which a System.Collections.Specialized.NotifyCollectionChangedAction.Move,
        //     Remove, or Replace action occurred.
        public int OldStartingIndex { get; private set; }
    }
}