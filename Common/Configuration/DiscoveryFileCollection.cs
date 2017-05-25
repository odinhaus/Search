using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Configuration
{
    [Serializable]
    public class DiscoveryFileCollection : IList<DiscoveryFileElement>
    {
        List<DiscoveryFileElement> _inner = new List<DiscoveryFileElement>();


        public int IndexOf(DiscoveryFileElement item)
        {
            return _inner.IndexOf(item);
        }

        public void Insert(int index, DiscoveryFileElement item)
        {
            _inner.Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            _inner.RemoveAt(index);
        }

        public DiscoveryFileElement this[int index]
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

        public void Add(DiscoveryFileElement item)
        {
            _inner.Add(item);
        }

        public void Clear()
        {
            _inner.Clear();
        }

        public bool Contains(DiscoveryFileElement item)
        {
            return _inner.Contains(item);
        }

        public void CopyTo(DiscoveryFileElement[] array, int arrayIndex)
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

        public bool Remove(DiscoveryFileElement item)
        {
            return _inner.Remove(item);
        }

        public IEnumerator<DiscoveryFileElement> GetEnumerator()
        {
            return _inner.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
