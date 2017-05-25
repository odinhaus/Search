using Data.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using Newtonsoft.Json;
using Data.Core.Linq;

namespace Data.ArangoDB
{
    public abstract class ModelBase : INotifyPropertyChanged, INotifyPropertyChanging
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public event PropertyChangingEventHandler PropertyChanging;
        protected virtual void OnPropertyChanging(string name)
        {
            if (PropertyChanging != null)
                PropertyChanging(this, new PropertyChangingEventArgs(name));

            //OnPropertyChangedInvoke(name);
        }

        public object _key { get; set; }
        public string _id { get; set; }
        public string _rev { get; set; }

        public long Key
        {
            get
            {
                if (_key == null)
                {
                    _key = 0L;
                }
                else if (_key is string)
                {
                    _key = long.Parse(_key.ToString());
                }
                return (long)_key;
            }

            set
            {
                _key = value;
            }
        }

        protected virtual void OnPropertyChanged(string name)
        {
            if (this.PropertyChanged != null)
            {
                this.PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        public bool IsNew
        {
            get { return Key == 0; }
        }
    }

    public abstract class LinkBase : ModelBase
    {
        string __from;
        public string _from
        {
            get { return __from; }
            set
            {
                __from = value;
                OnPropertyChanged("_from");
            }
        }

        string __to;
        public string _to
        {
            get { return __to; }
            set
            {
                __to = value;
                OnPropertyChanged("_to");
            }
        }

        string _TargetType;
        public string TargetType
        {
            get { return _TargetType; }
            set
            {
                _TargetType = value;
                OnPropertyChanged("TargetType");
            }
        }

        string _SourceType;
        public string SourceType
        {
            get { return _SourceType; }
            set
            {
                _SourceType = value;
                OnPropertyChanged("SourceType");
            }
        }

        IModel _fromModel;
        public IModel From
        {
            get { return _fromModel; }
            set
            {
                _fromModel = value;
                OnPropertyChanged("From");
            }
        }

        IModel _toModel;
        public IModel To
        {
            get { return _toModel; }
            set
            {
                _toModel = value;
                OnPropertyChanged("To");
            }

        }

        public abstract Type ModelType { get; }

        public bool IsDeleted
        {
            get;
            set;
        }

        public string GetKey()
        {
            return base.Key.ToString();
        }

        public void SetKey(string value)
        {
            base.Key = long.Parse(value);
        }
    }

    public static class ModelEx
    {
        public static string Id<T>(this T item) where T : IModel
        {
            var modelName = ModelCollectionManager.GetCollectionName(item is IPath ? ((IPath)item).Root.ModelType : item.ModelType);
            return modelName + "/" + item.GetKey();
        }
    }
}
