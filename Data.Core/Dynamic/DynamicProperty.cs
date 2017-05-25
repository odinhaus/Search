using Common;
using Common.Collections;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Dynamic
{
    public interface IPropertyDefinition { }
    public class Prop<T> : IPropertyDefinition
    {
        public Prop()
        {
            get = (value) => value;
            set = (value) => value;
        }
        public Func<T, T> get;
        public Func<T, T> set;
    }
    public class DynamicProperty<T> : INotifyPropertyChanging, INotifyPropertyChanged
    {
        object _value; // backing field value for the property
        public DynamicProperty() { }

        public DynamicProperty(T target, string propertyName, Func<object, object> getter, Func<object, object> setter)
        {
            this.TargetInstance = target;
            this.Name = propertyName;
            this.Type = typeof(T);

            this.Gettor = new Func<object>(() => { return getter(_value); });
            this.Settor = new Action<object>((object value) =>
            {
                OnPropertyChanging(this.Name);
                if (_value is INotifyPropertyChanged)
                {
                    ((INotifyPropertyChanged)_value).PropertyChanged -= DynamicProperty_PropertyChanged;
                }
                if (_value is INotifyPropertyChanging)
                {
                    ((INotifyPropertyChanging)_value).PropertyChanging -= DynamicProperty_PropertyChanging;
                }
                if (_value is INotifyCollectionChanged)
                {
                    ((INotifyCollectionChanged)_value).CollectionChanged -= DynamicProperty_CollectionChanged;
                }
                if (_value is INotifyCollectionChanging)
                {
                    ((INotifyCollectionChanging)_value).CollectionChanging -= DynamicProperty_CollectionChanging;
                }

                _value = setter(value);

                if (_value is INotifyPropertyChanged)
                {
                    ((INotifyPropertyChanged)_value).PropertyChanged += DynamicProperty_PropertyChanged;
                }
                if (_value is INotifyPropertyChanging)
                {
                    ((INotifyPropertyChanging)_value).PropertyChanging += DynamicProperty_PropertyChanging;
                }
                if (_value is INotifyCollectionChanged)
                {
                    ((INotifyCollectionChanged)_value).CollectionChanged += DynamicProperty_CollectionChanged;
                }
                if (_value is INotifyCollectionChanging)
                {
                    ((INotifyCollectionChanging)_value).CollectionChanging += DynamicProperty_CollectionChanging;
                }
                OnPropertyChanged(this.Name);
            });
        }

        public DynamicProperty(T target, string propertyName, object scalarValue)
            : this(target, propertyName, scalarValue.GetType())
        {
            this.Settor(scalarValue);
        }

        public DynamicProperty(T target, string propertyName, string type) : this(target, propertyName, TypeHelper.GetType(type)) { }

        public DynamicProperty(T target, string propertyName, Type type)
        {
            this.TargetInstance = target;
            this.Name = propertyName;
            this.Type = type;

            MemberInfo prop = null;
            object bi = null;
            if (target.GetType().BaseType.IsGenericType
                && target.GetType().BaseType.GetGenericTypeDefinition().Equals(typeof(Extendable<>)))
            {
                bi = target.GetType().GetProperty("BackingInstance").GetValue(target, null);
                if (bi == null)
                {
                    bi = target;
                }
            }

            if (bi != null)
            {
                prop = bi.GetType().GetMember(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).FirstOrDefault();
                if (prop != null
                    && !prop.DeclaringType.Equals(bi.GetType()))
                    prop = prop.DeclaringType.GetMember(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).FirstOrDefault();
            }

            if (prop == null)
            {
                this.Gettor = new Func<object>(() => { return _value; });
                this.Settor = new Action<object>((object value) => 
                {
                    OnPropertyChanging(this.Name);
                    if (_value is INotifyPropertyChanged)
                    {
                        ((INotifyPropertyChanged)_value).PropertyChanged -= DynamicProperty_PropertyChanged;
                    }
                    if (_value is INotifyPropertyChanging)
                    {
                        ((INotifyPropertyChanging)_value).PropertyChanging -= DynamicProperty_PropertyChanging;
                    }
                    if (_value is INotifyCollectionChanged)
                    {
                        ((INotifyCollectionChanged)_value).CollectionChanged -= DynamicProperty_CollectionChanged;
                    }
                    if (_value is INotifyCollectionChanging)
                    {
                        ((INotifyCollectionChanging)_value).CollectionChanging -= DynamicProperty_CollectionChanging;
                    }
                    _value = value;
                    if (_value is INotifyPropertyChanged)
                    {
                        ((INotifyPropertyChanged)_value).PropertyChanged += DynamicProperty_PropertyChanged;
                    }
                    if (_value is INotifyPropertyChanging)
                    {
                        ((INotifyPropertyChanging)_value).PropertyChanging += DynamicProperty_PropertyChanging;
                    }
                    if (_value is INotifyCollectionChanged)
                    {
                        ((INotifyCollectionChanged)_value).CollectionChanged += DynamicProperty_CollectionChanged;
                    }
                    if (_value is INotifyCollectionChanging)
                    {
                        ((INotifyCollectionChanging)_value).CollectionChanging += DynamicProperty_CollectionChanging;
                    }
                    OnPropertyChanged(this.Name);
                });
            }
            else
            {
                if (prop is PropertyInfo)
                {
                    this.Gettor = new Func<object>(delegate () { return ((PropertyInfo)prop).GetValue(bi, null); });
                    this.Settor = new Action<object>(delegate (object value) { ((PropertyInfo)prop).SetValue(bi, value, null); });
                }
                else if (prop is FieldInfo)
                {
                    this.Gettor = new Func<object>(delegate () { return ((FieldInfo)prop).GetValue(bi); });
                    this.Settor = new Action<object>(delegate (object value) { ((FieldInfo)prop).SetValue(bi, value); });
                }
            }
        }

        private void DynamicProperty_CollectionChanging(object sender, CollectionChangingEventArgs e)
        {
            OnPropertyChanging(FormatCollectionChangingString(this.Name, e));
        }

        private void DynamicProperty_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(FormatCollectionChangedString(this.Name, e));
        }

        private string FormatCollectionChangedString(string propertyName, NotifyCollectionChangedEventArgs e)
        {
            return string.Format("{0}.{1}({2},{3},{4},{5})",
                this.Name,
                e.Action,
                e.NewStartingIndex,
                e.NewItems?.Count ?? 0,
                e.OldStartingIndex,
                e.OldItems?.Count ?? 0
                );
        }

        private string FormatCollectionChangingString(string propertyName, CollectionChangingEventArgs e)
        {
            return string.Format("{0}.{1}({2},{3},{4},{5})",
                this.Name,
                e.Action,
                e.NewStartingIndex,
                e.NewItems?.Count ?? 0,
                e.OldStartingIndex,
                e.OldItems?.Count ?? 0
                );
        }

        private void DynamicProperty_PropertyChanging(object sender, PropertyChangingEventArgs e)
        {
            OnPropertyChanging(this.Name + "." + e.PropertyName);
        }

        private void DynamicProperty_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(this.Name + "." + e.PropertyName);
        }

        public DynamicProperty(T target, string instanceName, string propertyName, string type, string gettorCS, string settorCS, string bodyCS, string references)
            : this(target, propertyName, TypeHelper.GetType(type), DynamicPropertyEvaluatorBuilder.Create(target, instanceName, propertyName, gettorCS, settorCS, bodyCS, references))
        { }

        public DynamicProperty(T target, string propertyName, Type type, IDynamicPropertyEvaluator evaluator)
        {
            this.TargetInstance = target;
            this.Name = propertyName;
            this.Type = type;
            this.Gettor = evaluator.Gettor;
            this.Settor = evaluator.Settor;
            evaluator.PropertyChanged += new PropertyChangedEventHandler(evaluator_PropertyChanged);
        }

        void evaluator_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (this.PropertyChanged != null)
            {
                this.PropertyChanged(this, e);
            }
        }

        public T TargetInstance { get; private set; }
        public string Name { get; private set; }
        public Type Type { get; private set; }

        private Func<object> Gettor;
        private Action<System.Object> Settor;

        public object Value { get { return this.Gettor(); } set { this.Settor(value); } }

        public event PropertyChangedEventHandler PropertyChanged;
        public event PropertyChangingEventHandler PropertyChanging;

        [ThreadStatic]
        static HashSet<object> _isPropertyChanged = new HashSet<object>();

        protected void OnPropertyChanged(string name)
        {
            if (!_isPropertyChanged.Contains(this))
            {
                _isPropertyChanged.Add(this);
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged(this, new PropertyChangedEventArgs(name));
                }
                _isPropertyChanged.Remove(this);
            }
        }

        [ThreadStatic]
        static HashSet<object> _isPropertyChanging = new HashSet<object>();
        protected void OnPropertyChanging(string name)
        {
            if (!_isPropertyChanging.Contains(this))
            {
                _isPropertyChanging.Add(this);
                if (this.PropertyChanging != null)
                {
                    this.PropertyChanging(this, new PropertyChangingEventArgs(name));
                }
                _isPropertyChanging.Remove(this);
            }
        }
    }
}
