using Common.Extensions;
using Common.Serialization;
using Common.Serialization.Binary;
using Data.Core;
using Data.Core.Compilation;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.ArangoDB
{
    public abstract class TrackedModelBase : ModelBase, IProxyModel, IBinarySerializable
    {
        protected TrackedModelBase(IModel current, IRepository repository)
        {
            this.Current = current;
            ((INotifyPropertyChanging)Current).PropertyChanging += Current_PropertyChanging;
            ((INotifyPropertyChanged)Current).PropertyChanged += Current_PropertyChanged;
            this.State = TrackingState.Unknown;
            this.Repository = repository;
            this.WorkContextKeys = new List<string>();
        }

        private void Current_PropertyChanging(object sender, PropertyChangingEventArgs e)
        {
            if (object.ReferenceEquals(Current, Committed))
            {
                Commit();
            }
            this.State = this.State == TrackingState.ShouldDelete ? this.State : TrackingState.Unknown;
            OnPropertyChanging(e.PropertyName);
        }

        private void Current_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(e.PropertyName);
        }

        IModel _committed = null;
        IModel _current = null;
        public IModel Committed
        {
            get
            {
                if (_committed == null)
                    return _current;
                else
                    return _committed;
            }
            private set
            {
                _committed = value;
            }
        }

        public IModel Current
        {
            get { return _current; }
            private set
            {
                _current = value;
            }
        }

        public Type ModelType
        {
            get
            {
                return Current.ModelType;
            }
        }

        public TrackingState State
        {
            get;
            set;
        }

        public bool IsObserving
        {
            get;
            set;
        }

        public int ReferenceCount
        {
            get;
            set;
        }
        public IRepository Repository { get; private set; }

        public bool IsValid
        {
            get
            {
                return this.Repository.IsValid && !this.Committed.IsDeleted;
            }
        }

        public IList<string> WorkContextKeys { get; private set; }

        protected virtual void CloneDynamicItems(object source, object dest) { }



        public void Commit()
        {
            Committed = Clone(Current);
            RaisePropertyChangedEvents();
            this.State = TrackingState.IsUnchanged;
        }

        public void Commit(IModel model)
        {
            if (Current != null)
            {
                ((INotifyPropertyChanged)Current).PropertyChanged -= Current_PropertyChanged;
                ((INotifyPropertyChanged)Current).PropertyChanged -= Current_PropertyChanged;
            }
            Current = model;
            Committed = Clone(Current);
            RaisePropertyChangedEvents();
            this.State = TrackingState.IsUnchanged;
            ((INotifyPropertyChanged)Current).PropertyChanged += Current_PropertyChanged;
            ((INotifyPropertyChanged)Current).PropertyChanged += Current_PropertyChanged;
        }


        public void Revert()
        {
            ((INotifyPropertyChanged)Current).PropertyChanged -= Current_PropertyChanged;
            ((INotifyPropertyChanged)Current).PropertyChanged -= Current_PropertyChanged;
            Current = Clone(Committed);
            RaisePropertyChangedEvents();
            this.State = TrackingState.Unknown;
            ((INotifyPropertyChanged)Current).PropertyChanged += Current_PropertyChanged;
            ((INotifyPropertyChanged)Current).PropertyChanged += Current_PropertyChanged;
        }

        protected void RaisePropertyChangedEvents()
        {
            OnPropertyChanged("Name");
            OnPropertyChanged("Baseline");
            OnPropertyChanged("Current");
            OnPropertyChanged("Committed");

            RaiseModelPropertyChangedEvents();
        }

        protected abstract void RaiseModelPropertyChangedEvents();


        protected IModel Clone(IModel source)
        {
            var clone = (IModel)RuntimeModelBuilder.CreateModelInstance(source.ModelType);
            ((IBinarySerializable)clone).FromBytes(((IBinarySerializable)source).ToBytes());

            CloneIModelProperties(source, clone);
            CloneEnumerableIModelProperties(source, clone);

            return clone;
        }

        protected abstract void CloneIModelProperties(IModel source, IModel destination);
        protected abstract void CloneEnumerableIModelProperties(IModel source, IModel destination);


        public string GetKey()
        {
            return Current.GetKey();
        }

        public void SetKey(string value)
        {
            Current.SetKey(value);
        }

        public TrackingState CalculateState(bool forceEvaluation = false)
        {
            if (this.State == TrackingState.Unknown || forceEvaluation)
            {
                if (object.ReferenceEquals(Committed, Current))
                {
                    this.State = TrackingState.IsUnchanged;
                }
                else if (Current.IsDeleted)
                {
                    this.State = TrackingState.ShouldDelete;
                }
                else if (string.IsNullOrEmpty(GetKey()) || GetKey().Equals("0"))
                {
                    this.State = TrackingState.ShouldSave;
                }
                else
                {
                    // theres probably a faster way to do this....
                    var currentCksum = Current.ToJson().ToBase64MD5();
                    var committedCksum = Committed.ToJson().ToBase64MD5();
                    if (currentCksum.Equals(committedCksum))
                    {
                        this.State = TrackingState.IsUnchanged;
                    }
                    else
                    {
                        this.State = TrackingState.ShouldSave;
                    }
                }
            }

            return this.State;
        }

        public byte[] ProtocolBuffer
        {
            get
            {
                return ((IBinarySerializable)Current).ProtocolBuffer;
            }

            set
            {
                ((IBinarySerializable)Current).ProtocolBuffer = value;
            }
        }

        public byte[] ToBytes()
        {
            return ((IBinarySerializable)Current).ToBytes();
        }

        public void FromBytes(byte[] source)
        {
            ((IBinarySerializable)Current).FromBytes(source);
        }
    }
}
