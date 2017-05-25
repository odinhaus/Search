using Altus.Suffūz.Serialization;
using Common.Application;
using Common.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Common.DI;

namespace Common.Serialization
{
    public abstract class SerializerBase : IInitialize, ISerializer
    {
        protected abstract IEnumerable<Type> OnGetSupportedTypes();
        protected abstract byte[] OnSerialize(object source);
        protected abstract object OnDeserialize(byte[] source, Type targetType);
        protected abstract bool OnSupportsFormats(string format);
        protected virtual bool OnIsScalar() { return false; }


        protected virtual void OnDispose()
        {
        }


        IEnumerable<Type> _types = null;
        public IEnumerable<Type> SupportedTypes
        {
            get { return _types; }
        }

        public void Initialize(string name, params string[] args)
        {
            this.Name = name;
            this._types = OnGetSupportedTypes();
        }

        public bool IsInitialized
        {
            get;
            private set;
        }

        public bool IsEnabled
        {
            get;
            set;
        }

        public bool IsRegistered
        {
            get;
            protected set;
        }

        public int Priority { get; private set; }

        public string Name { get; private set; }

        public bool IsScalar { get { return OnIsScalar(); } }

        public bool SupportsFormat(string format)
        {
            return OnSupportsFormats(format);
        }

        public bool SupportsType(Type type)
        {
            return _types != null && _types.Contains(type);
        }

        public byte[] Serialize(object source)
        {
            return OnSerialize(source);
        }

        public object Deserialize(byte[] source, Type targetType)
        {
            return OnDeserialize(source, targetType);
        }

        public event EventHandler Disposed;

        public System.ComponentModel.ISite Site
        {
            get;
            set;
        }

        public void Dispose()
        {
            this.OnDispose();
            if (Disposed != null)
                Disposed(this, new EventArgs());
        }

        public abstract void Register(IContainerMappings mappings);
    }

    public abstract class SerializerBase<T> : SerializerBase, ISerializer<T>
    {
        public byte[] Serialize(T source)
        {
            return base.Serialize(source);
        }

        public void Serialize(T source, System.IO.Stream outputStream)
        {
            StreamHelper.Copy(Serialize(source), outputStream);
        }

        public T Deserialize(byte[] source)
        {
            return (T)base.Deserialize(source, typeof(T));
        }

        public T Deserialize(System.IO.Stream inputSource)
        {
            return Deserialize(StreamHelper.GetBytes(inputSource));
        }

        protected override IEnumerable<Type> OnGetSupportedTypes()
        {
            return new Type[] { typeof(T) };
        }
    }
}
