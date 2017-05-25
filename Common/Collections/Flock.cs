using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Collections
{
    public class Flock<T> : IList<T>, INotifyCollectionChanged, INotifyCollectionChanging, IList, IDisposable
    {
        public event NotifyCollectionChangedEventHandler CollectionChanged;
        public event CollectionChangingHandler CollectionChanging;
        private List<T> _inner = new List<T>();
        private string _id = Guid.NewGuid().ToString();

        public Flock()
        {
        }

        public Flock(IEnumerable<T> items)
        {
            foreach (var i in items)
                _inner.Add(i);
         }

        public T this[int index]
        {
            get
            {
                return _inner[index];
            }

            set
            {
                var oldItems = new T[] { _inner[index] };
                var newItems = new T[] { value };

                OnCollectionChanging(NotifyCollectionChangedAction.Replace, oldItems, newItems, index, index);

                _inner[index] = value;

                OnCollectionChanged(NotifyCollectionChangedAction.Replace, oldItems, newItems, index, index);
            }
        }

        public int Count
        {
            get
            {
                return _inner.Count;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return false;
            }
        }

        bool IList.IsReadOnly
        {
            get
            {
                return this.IsReadOnly;
            }
        }

        bool IList.IsFixedSize
        {
            get
            {
                return false;
            }
        }

        int ICollection.Count
        {
            get
            {
                return this.Count;
            }
        }

        object ICollection.SyncRoot
        {
            get
            {
                return _inner;
            }
        }

        bool ICollection.IsSynchronized
        {
            get
            {
                return false;
            }
        }

        object IList.this[int index]
        {
            get
            {
                return this[index];
            }

            set
            {
                this[index] = (T)value;
            }
        }

        protected virtual void OnCollectionChanging(NotifyCollectionChangedAction action, IEnumerable<T> oldItems, IEnumerable<T> newItems, int oldIndex, int newIndex)
        {
            if (CollectionChanging != null)
            {
                CollectionChanging(this, new CollectionChangingEventArgs(action, newItems.ToList(), oldItems.ToList(), newIndex, oldIndex));
            }
        }

        protected virtual void OnCollectionChanged(NotifyCollectionChangedAction action, IEnumerable<T> oldItems, IEnumerable<T> newItems, int oldIndex, int newIndex)
        {
            if (CollectionChanged != null)
            {
                if (action == NotifyCollectionChangedAction.Replace)
                {
                    CollectionChanged(this, new NotifyCollectionChangedEventArgs(action, newItems.FirstOrDefault(), oldItems.FirstOrDefault()));
                }
                else if (action == NotifyCollectionChangedAction.Reset)
                {
                    CollectionChanged(this, new NotifyCollectionChangedEventArgs(action));
                }
                else
                {
                    CollectionChanged(this, new NotifyCollectionChangedEventArgs(action,
                        action == NotifyCollectionChangedAction.Add ? newItems.ToList() : oldItems.ToList(),
                        action == NotifyCollectionChangedAction.Add ? newIndex : oldIndex));
                }
            }
        }

        public void Add(T item)
        {
            var newItems = new T[] { item };
            var oldItems = new T[0];

            OnCollectionChanging(NotifyCollectionChangedAction.Add, oldItems, newItems, -1, _inner.Count);
            _inner.Add(item);
            OnCollectionChanged(NotifyCollectionChangedAction.Add, oldItems, newItems, -1, _inner.Count - 1);
        }

        public void AddRange(IEnumerable<T> items)
        {
            foreach (var i in items)
                _inner.Add(i);
        }

        public void Clear()
        {
            var newItems = new T[0];
            var oldItems = new T[0];

            OnCollectionChanging(NotifyCollectionChangedAction.Reset, oldItems, newItems, -1, -1);
            Dispose();
            OnCollectionChanged(NotifyCollectionChangedAction.Reset, oldItems, newItems, -1, -1);
        }

        public bool Contains(T item)
        {
            return _inner.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            _inner.CopyTo(array, arrayIndex);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _inner.GetEnumerator();
        }

        public int IndexOf(T item)
        {
            return _inner.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            var newItems = new T[] { item };
            var oldItems = new T[0];

            OnCollectionChanging(NotifyCollectionChangedAction.Add, oldItems, newItems, -1, index);
            _inner.Insert(index, item);
            OnCollectionChanged(NotifyCollectionChangedAction.Add, oldItems, newItems, -1, index);
        }

        public bool Remove(T item)
        {
            var newItems = new T[0];
            var oldItems = new T[] { item };
            var oldIndex = _inner.IndexOf(item);

            OnCollectionChanging(NotifyCollectionChangedAction.Remove, oldItems, newItems, oldIndex , -1);
            var removed = _inner.Remove(item);
            if (removed)
            {
                OnCollectionChanged(NotifyCollectionChangedAction.Remove, oldItems, newItems, oldIndex, -1);
            }
            return removed;
        }

        public void RemoveAt(int index)
        {
            var newItems = new T[0];
            var oldItems = new T[] { _inner[index] };

            OnCollectionChanging(NotifyCollectionChangedAction.Remove, oldItems, newItems, index, -1);
            _inner.RemoveAt(index);
            OnCollectionChanged(NotifyCollectionChangedAction.Remove, oldItems, newItems, index, -1);
        }



        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        int IList.Add(object value)
        {
            this.Add((T)value);
            return 1;
        }

        bool IList.Contains(object value)
        {
            return this.Contains((T)value);
        }

        void IList.Clear()
        {
            this.Clear();
        }

        int IList.IndexOf(object value)
        {
            return this.IndexOf((T)value);
        }

        void IList.Insert(int index, object value)
        {
            this.Insert(index, (T)value);
        }

        void IList.Remove(object value)
        {
            this.Remove((T)value);
        }

        void IList.RemoveAt(int index)
        {
            this.RemoveAt(index);
        }

        void ICollection.CopyTo(Array array, int index)
        {
            this.CopyTo((T[])array, index);
        }

        public void Dispose()
        {
            _inner.Clear();
        }

        private void Flock_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (this.CollectionChanged != null)
            {
                this.CollectionChanged(sender, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, new T[] { (T)sender }, new T[] { (T)sender }, _inner.IndexOf((T)sender)));
            }
        }

        private void Flock_PropertyChanging(object sender, PropertyChangingEventArgs e)
        {
            if (this.CollectionChanging != null)
            {
                this.CollectionChanging(sender, 
                    new CollectionChangingEventArgs(NotifyCollectionChangedAction.Replace, 
                                                        new T[] { (T)sender }, 
                                                        new T[] { (T)sender }, 
                                                        _inner.IndexOf((T)sender), 
                                                        _inner.IndexOf((T)sender)));
            }
        }

        public void Sort(Comparison<T> comparison)
        {
            _inner.Sort(comparison);
        }

        public void Sort(IComparer<T> comparer)
        {
            _inner.Sort(comparer);
        }
    }
}
