using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Configuration
{
    [Serializable]
    public class DiscoveryManifestTargetCollection : IList<DiscoveryTarget>
    {
        List<DiscoveryTarget> _inner = new List<DiscoveryTarget>();

        public int IndexOf(DiscoveryTarget item)
        {
            return _inner.IndexOf(item);
        }

        public void Insert(int index, DiscoveryTarget item)
        {
            _inner.Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            _inner.RemoveAt(index);
        }

        public DiscoveryTarget this[int index]
        {
            get
            {
                return _inner[index];
            }
            set
            {
                _inner[index] = value;
            }
        }

        public void Add(DiscoveryTarget item)
        {
            _inner.Add(item);
        }

        public void Clear()
        {
            _inner.Clear();
        }

        public bool Contains(DiscoveryTarget item)
        {
            return _inner.Contains(item);
        }

        public void CopyTo(DiscoveryTarget[] array, int arrayIndex)
        {
            _inner.CopyTo(array, arrayIndex);
        }

        public int Count
        {
            get { return _inner.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool Remove(DiscoveryTarget item)
        {
            return _inner.Remove(item);
        }

        public IEnumerator<DiscoveryTarget> GetEnumerator()
        {
            return _inner.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
