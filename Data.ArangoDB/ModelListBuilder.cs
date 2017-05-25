using Data.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using Data.Core.Compilation;
using System.Reflection;

namespace Data.ArangoDB
{
    public class ModelListBuilder<T> where T : IModel
    {
        public ModelList<T> Create(List<Dictionary<string, object>> arangoDocuments, long offset, long totalRecords, int pageSize)
        {
            return new ModelList<T>(new DocumentConverter(arangoDocuments), offset, totalRecords, arangoDocuments?.Count??0, pageSize);
        }

        class DocumentConverter : IList<T>
        {
            IList<T> _items = new List<T>();
            public DocumentConverter(List<Dictionary<string, object>> arangoDocuments)
            {
                this.Documents = arangoDocuments;
                if (typeof(T) == typeof(IAny))
                {
                    this.Activator = (dict) =>
                    {
                        if (dict == null) return default(T);

                        var modelName = dict["ModelType"].ToString();
                        var modelType = ModelTypeManager.GetModelType(modelName);
                        return (T)RuntimeModelBuilder.CreateModelInstanceActivator(modelType)(dict);
                    };
                }
                else
                {
                    this.Activator = RuntimeModelBuilder.CreateModelInstanceActivator<T>();
                }

                foreach(var item in this)
                {
                    _items.Add(item);
                }
            }

            public T this[int index]
            {
                get
                {
                    return _items[index];
                }

                set
                {
                    _items[index] = value;
                }
            }

            public Func<Dictionary<string, object>, T> Activator { get; private set; }

            public int Count
            {
                get
                {
                    return _items.Count;
                }
            }

            public List<Dictionary<string, object>> Documents { get; private set; }

            public bool IsReadOnly
            {
                get
                {
                    return false;
                }
            }

            public void Add(T item)
            {
                _items.Add(item);
            }

            public void Clear()
            {
                _items.Clear();
            }

            public bool Contains(T item)
            {
                return _items.Contains(item);
            }

            public void CopyTo(T[] array, int arrayIndex)
            {
                _items.CopyTo(array, arrayIndex);
            }

            public IEnumerator<T> GetEnumerator()
            {
                foreach(var doc in Documents?? new List<Dictionary<string, object>>())
                {
                    yield return this.Activator(doc);
                }
            }

            public int IndexOf(T item)
            {
                return _items.IndexOf(item);
            }

            public void Insert(int index, T item)
            {
                _items.Insert(index, item);
            }

            public bool Remove(T item)
            {
                return _items.Remove(item);
            }

            public void RemoveAt(int index)
            {
                _items.RemoveAt(index);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }
}
