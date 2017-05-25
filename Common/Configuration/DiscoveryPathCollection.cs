using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Configuration
{
    [Serializable]
    public class DiscoveryPathCollection : ConfigurationElementCollection
    {
        protected override ConfigurationElement CreateNewElement()
        {
            return new DiscoveryPathElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            DiscoveryPathElement service = (DiscoveryPathElement)element;

            return getKey(service);
        }

        public void Sort()
        {
            List<DiscoveryPathElement> sorted = new List<DiscoveryPathElement>();
            foreach (DiscoveryPathElement e in this)
            {
                sorted.Add(e);
            }
            sorted.Sort();
            this.Clear();
            foreach (DiscoveryPathElement e in sorted)
            {
                this.Add(e);
            }
        }

        /// <summary>
        /// Gets or sets the named service element for the given index.
        /// </summary>
        /// <param name="index">The index of the named service element to get or set.</param>
        /// <returns>The named service element.</returns>
        public DiscoveryPathElement this[int index]
        {
            get
            {
                return (DiscoveryPathElement)BaseGet(index);
            }
            set
            {
                if (BaseGet(index) != null)
                {
                    BaseRemove(index);
                }
                BaseAdd(index, value);
            }
        }

        /// <summary>
        /// Gets or sets the named service element for the given name.
        /// </summary>
        /// <param name="name">The name of the named service element to get or set.</param>
        /// <returns>The named service element.</returns>
        public new DiscoveryPathElement this[string name]
        {
            get
            {
                return (DiscoveryPathElement)BaseGet(name);
            }
        }

        /// <summary>
        /// Gets the number of named service elements in this instance.
        /// </summary>
        public new int Count
        {
            get { return base.Count; }
        }

        public int IndexOf(DiscoveryPathElement service)
        {
            return BaseIndexOf(service);
        }

        public void RemoveAt(int index)
        {
            BaseRemoveAt(index);
        }

        public void Add(DiscoveryPathElement item)
        {
            BaseAdd(item);
        }

        public void Clear()
        {
            BaseClear();
        }

        public bool Contains(DiscoveryPathElement item)
        {
            return BaseIndexOf(item) >= 0;
        }

        public void CopyTo(DiscoveryPathElement[] array, int arrayIndex)
        {
            base.CopyTo(array, arrayIndex);
        }

        public new bool IsReadOnly
        {
            get { return false; }
        }

        public bool Remove(DiscoveryPathElement item)
        {
            if (BaseIndexOf(item) >= 0)
            {
                BaseRemove(item);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Gets the key by which named service elements are mapped in the base class.
        /// </summary>
        /// <param name="service">The named service element to get the key from.</param>
        /// <returns>The key.</returns>
        private string getKey(DiscoveryPathElement service)
        {
            return service.Path;
        }
    }
}
