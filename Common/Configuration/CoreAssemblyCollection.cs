using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Configuration
{
    [Serializable]
    public class CoreAssemblyCollection : ConfigurationElementCollection
    {
        protected override ConfigurationElement CreateNewElement()
        {
            return new CoreAssemblyElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            CoreAssemblyElement service = (CoreAssemblyElement)element;

            return getKey(service);
        }

        public void Sort()
        {
            List<CoreAssemblyElement> sorted = new List<CoreAssemblyElement>();
            foreach (CoreAssemblyElement e in this)
            {
                sorted.Add(e);
            }
            sorted.Sort();
            this.Clear();
            foreach (CoreAssemblyElement e in sorted)
            {
                this.Add(e);
            }
        }

        /// <summary>
        /// Gets or sets the named service element for the given index.
        /// </summary>
        /// <param name="index">The index of the named service element to get or set.</param>
        /// <returns>The named service element.</returns>
        public CoreAssemblyElement this[int index]
        {
            get
            {
                return (CoreAssemblyElement)BaseGet(index);
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
        public new CoreAssemblyElement this[string name]
        {
            get
            {
                return (CoreAssemblyElement)BaseGet(name);
            }
        }

        /// <summary>
        /// Gets the number of named service elements in this instance.
        /// </summary>
        public new int Count
        {
            get { return base.Count; }
        }

        public int IndexOf(CoreAssemblyElement service)
        {
            return BaseIndexOf(service);
        }

        public void RemoveAt(int index)
        {
            BaseRemoveAt(index);
        }

        public void Add(CoreAssemblyElement item)
        {
            BaseAdd(item);
        }

        public void Clear()
        {
            BaseClear();
        }

        public bool Contains(CoreAssemblyElement item)
        {
            return BaseIndexOf(item) >= 0;
        }

        public void CopyTo(CoreAssemblyElement[] array, int arrayIndex)
        {
            base.CopyTo(array, arrayIndex);
        }

        public new bool IsReadOnly
        {
            get { return false; }
        }

        public bool Remove(CoreAssemblyElement item)
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
        private string getKey(CoreAssemblyElement service)
        {
            return Path.Combine(service.Path, service.Assembly);
        }
    }
}
