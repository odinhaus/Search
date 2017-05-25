using Common;
using Common.Collections;
using Common.Serialization.Binary;
using Data.Core.Compilation;
using Data.Core.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core
{
    public class ModelList<T> : IList<T>, IModelList, IBinarySerializable, IList
    {
        IList<T> _items = new Flock<T>();
        public ModelList() { }

        public ModelList(IList<T> items, long offset, long totalRecords, int pageCount, int pageSize)
        {
            _items = items.ToList();
            this.Offset = offset;
            this.TotalRecords = totalRecords;
            this.PageSize = pageSize;
            this.PageCount = pageCount;
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

        public int Count
        {
            get
            {
                return (int)TotalRecords;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return false;
            }
        }

        public long Offset { get; private set; }

        public int PageCount { get; private set; }

        public int PageSize { get; private set; }

        public byte[] ProtocolBuffer
        {
            get;

            set;
        }

        public long TotalRecords { get; private set; }

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
                return this;
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

        public void Add(T item)
        {
            _items.Add(item);
        }

        public void Clear()
        {
            _items.Clear();
            TotalRecords = 0;
            Offset = 0;
        }

        public bool Contains(T item)
        {
            return _items.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            _items.CopyTo(array, arrayIndex);
        }

        

        public virtual IEnumerator<T> GetEnumerator()
        {
            return _items.GetEnumerator();
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

        public byte[] ToBytes()
        {
            using (var ms = new MemoryStream())
            {
                using (var bw = new BinaryWriter(ms))
                {
                    bw.Write(Offset);
                    bw.Write(PageSize);
                    bw.Write(PageCount);
                    bw.Write(TotalRecords);
                    if (typeof(T) == typeof(IAny))
                    {
                        bw.Write(true);
                        var groups = _items.OfType<IModel>().Select((model, index) => new { Model = model, Index = index })
                                                            .GroupBy(i => i.Model.ModelType);
                        bw.Write(groups.Count());
                        foreach (var group in groups)
                        {
                            bw.Write(ModelTypeManager.GetModelName(group.Key));
                            bw.Write(group.Count());
                            foreach (var item in group)
                            {
                                var bytes = ((IBinarySerializable)item.Model).ToBytes();
                                bw.Write(item.Index);
                                bw.Write(bytes.Length);
                                bw.Write(bytes);
                            }
                        }
                    }
                    else
                    {
                        bw.Write(false);
                        for (int i = 0; i < PageCount; i++)
                        {
                            var bytes = ((IBinarySerializable)_items[i]).ToBytes();
                            bw.Write(bytes.Length);
                            bw.Write(bytes);
                        }
                    }

                    return ms.ToArray();
                }
            }
        }

        public void FromBytes(byte[] source)
        {
            using (var ms = new MemoryStream(source))
            {
                using (var br = new BinaryReader(ms))
                {
                    this._items = new List<T>();
                    this.Offset = br.ReadInt64();
                    this.PageSize = br.ReadInt32();
                    this.PageCount = br.ReadInt32();
                    this.TotalRecords = br.ReadInt64();
                    if (br.ReadBoolean())
                    {
                        var groupCount = br.ReadInt32();
                        var list = new T[this.TotalRecords];
                        for(int g = 0; g < groupCount; g++)
                        {
                            var modelName = br.ReadString();
                            var modelType = ModelTypeManager.GetModelType(modelName);
                            var count = br.ReadInt32();
                            var isPath = modelType.Implements<IPath>();
                            for(int i = 0; i < count; i++)
                            {
                                var index = br.ReadInt32();
                                var length = br.ReadInt32();
                                var bytes = br.ReadBytes(length);
                                var item = isPath
                                    ? (IBinarySerializable)Activator.CreateInstance(modelType) 
                                    : (IBinarySerializable)RuntimeModelBuilder.CreateModelInstance(modelType);
                                item.FromBytes(bytes);
                                if (isPath && !typeof(T).Implements<IPath>() && !typeof(T).Equals(typeof(IAny)))
                                {
                                    list[index] = (T)((IPath)item).Root;
                                }
                                else
                                {
                                    list[index] = (T)item;
                                }
                            }
                        }
                        ((List<T>)_items).AddRange(list);
                    }
                    else
                    {
                        for (int i = 0; i < PageCount; i++)
                        {
                            var item = typeof(T).Implements<IModel>() ? RuntimeModelBuilder.CreateModelInstance<T>() : Activator.CreateInstance<T>();
                            var length = br.ReadInt32();
                            var bytes = br.ReadBytes(length);
                            ((IBinarySerializable)item).FromBytes(bytes);
                            _items.Add(item);
                        }
                    }
                }
            }
        }

        int IList.Add(object value)
        {
            this.Add((T)value);
            return _items.Count - 1;
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
    }
}
