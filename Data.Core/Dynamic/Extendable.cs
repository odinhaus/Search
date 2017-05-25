using Newtonsoft.Json;
using Common;
using Common.Application;
using Common.Diagnostics;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Dynamic
{
    public interface IAliased
    {
        IEnumerable<string> Aliases { get; }
    }


    public delegate bool MemberResolutionHandler(string memberName, out object resolvedResult);
    public abstract class Extendable<T> : DynamicObject, INotifyPropertyChanged, INotifyPropertyChanging, IAliased, IExtendable<T> where T : Extendable<T>, IDynamicMetaObjectProvider
    {
        protected string _instanceId = Guid.NewGuid().ToString();

        protected Dictionary<string, DynamicProperty<T>> PropertyBag = new Dictionary<string, DynamicProperty<T>>();
        protected Dictionary<string, DynamicFunction<T>> FunctionBag = new Dictionary<string, DynamicFunction<T>>();
        protected IDynamicFunctionEvaluator FunctionEvaluator;
        protected HashSet<string> AliasBag = new HashSet<string>();

        [JsonIgnore]
        public IEnumerable<string> Aliases
        {
            get
            {
                OnExtend();
                return AliasBag;
            }
        }


        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string name)
        {
            OnPropertyChanged(this, new PropertyChangedEventArgs(name));
        }

        protected virtual void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (PropertyChanged != null)
                PropertyChanged(sender, e);
        }

        public event PropertyChangingEventHandler PropertyChanging;
        protected virtual void OnPropertyChanging(string name)
        {
            if (PropertyChanging != null)
                PropertyChanging(this, new PropertyChangingEventArgs(name));

            //OnPropertyChangedInvoke(name);
        }


        public Extendable()
        {
            this.IsExtendable = true;
        }
        public Extendable(string instanceName, object backingInstance) : this(instanceName, backingInstance, true)
        {
        }
        public Extendable(string instanceName, object backingInstance, bool isExtendable)
        {
            this.OnSetBackingInstance(backingInstance, true);
            this.InstanceName = instanceName;
            this.IsExtendable = isExtendable;
            this.MemberResolutionFailedCallback = new MemberResolutionHandler(DefaultMemberResolutionFailure);
        }
        public Extendable(string instanceName, object backingInstance, bool isExtendable, MemberResolutionHandler memberResolutionFailedCallback)
        {
            this.OnSetBackingInstance(backingInstance, true);
            this.InstanceName = instanceName;
            this.IsExtendable = isExtendable;
            this.MemberResolutionFailedCallback = memberResolutionFailedCallback;
        }

        protected virtual bool DefaultMemberResolutionFailure(string memberName, out object result)
        {
            result = null;
            return false;
        }

        protected MemberResolutionHandler MemberResolutionFailedCallback;

        protected virtual bool OnInitialize() { return true; }

        protected HashSet<DeclaredApp> _extensionsLoaded = new HashSet<DeclaredApp>();
        protected virtual void OnExtend()
        {
            if (this.IsExtendable)
            {
                if (AppContext.Current == null) return;

                if (!_extensionsLoaded.Contains(AppContext.Current.CurrentApp))
                {
                    this.SetFunctions();
                    this.SetProperties();
                    this.SetAliases();
                    _extensionsLoaded.Add(AppContext.Current.CurrentApp);
                }
            }
        }

        protected abstract IEnumerable<string> OnGetAliases();
        protected virtual void SetAliases()
        {
            foreach (string alias in this.OnGetAliases())
            {
                if (!this.AliasBag.Contains(alias))
                    this.AliasBag.Add(alias);
            }
        }

        public virtual IEnumerable<DynamicProperty<T>> GetProperties
        {
            get
            {
                OnExtend();
                return PropertyBag.Values;
            }
        }

        protected abstract IEnumerable<DynamicProperty<T>> OnGetProperties();
        protected virtual void SetProperties()
        {
            foreach (DynamicProperty<T> dp in this.OnGetProperties())
            {
                this.PropertyBag.Add(dp.Name, dp);
                string cmdPrefix = string.Concat("OnSet", dp.Name);
                dp.PropertyChanged += new PropertyChangedEventHandler(dp_PropertyChanged);
            }
        }

        public bool HasProperty(string name)
        {
            return PropertyBag.Any(p => p.Key.Equals(name)) 
                || this.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Any(m => m.Name.Equals(name))
                || (
                    this.BackingInstance != null 
                    && this.BackingInstance.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Any(m => m.Name.Equals(name)));
        }

        public bool HasMethod(string name)
        {
            return FunctionBag.Any(f => f.Key.Equals(name))
                || this.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Any(p => p.Name.Equals(name))
                || (
                    this.BackingInstance != null
                    && this.BackingInstance.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Any(p => p.Name.Equals(name)));
        }

        public bool HasAlias(string name)
        {
            return AliasBag.Any(a => a.Equals(name));
        }

        public virtual void AddProperty<U>(string name)
        {
            AddProperty(new DynamicProperty<T>((T)this, name, typeof(U)));
        }

        public virtual void AddProperty(string name, object scalarValue)
        {
            if (scalarValue.GetType().Implements<IPropertyDefinition>())
            {
                var propertyType = scalarValue.GetType().GetGenericArguments()[0];
                var pName = Expression.Parameter(typeof(string));
                var prop = Expression.Parameter(scalarValue.GetType());
                
                var method = this.GetType().GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                    .Single(m => m.Name.Equals("AddProperty") && m.IsGenericMethod && m.GetParameters().Length == 2)
                    .MakeGenericMethod(propertyType);
                var lambdaType = typeof(Action<,>).MakeGenericType(typeof(string), typeof(Prop<>).MakeGenericType(propertyType));

                Expression.Lambda(lambdaType,
                    Expression.Call(Expression.Constant(this), method, pName, prop), pName, prop).Compile().DynamicInvoke(name, scalarValue);
            }
            else if (scalarValue.GetType().IsGenericType && scalarValue.GetType().GetGenericTypeDefinition().Equals(typeof(Func<>)))
            {
                var del = (Delegate)scalarValue;
                AddProperty(new DynamicProperty<T>((T)this, name, (value) => del.DynamicInvoke(), (value) => value));
            }
            else
            {
                AddProperty(new DynamicProperty<T>((T)this, name, scalarValue));
            }
        }

        protected virtual void AddProperty<U>(string name, Prop<U> prop)
        {
            AddProperty<U>(name, prop.get, prop.set);
        }

        public virtual void AddProperty<U>(string name, Func<U,U> getter, Func<U,U> setter)
        {
            AddProperty(new DynamicProperty<T>(
                (T)this, 
                name, 
                (value) => 
                {
                    U cast;
                    if (value.TryCast<U>(out cast))
                    {
                        return getter(cast);
                    }
                    else
                    {
                        return getter(default(U));
                    }
                }, 
                (value) =>
                {
                    U cast;
                    if (value.TryCast<U>(out cast))
                    {
                        return setter(cast);
                    }
                    else
                    {
                        return setter(default(U));
                    }
                }));
        }

        public virtual void AddProperty<U>(string name, Func<U, U> setter)
        {
            AddProperty(new DynamicProperty<T>(
                (T)this, 
                name, 
                (value) => value,
                (value) =>
                {
                    U cast;
                    if (value.TryCast<U>(out cast))
                    {
                        return setter(cast);
                    }
                    else
                    {
                        return setter(default(U));
                    }
                }));
        }

        public virtual void AddProperty(DynamicProperty<T> dp)
        {
            this.PropertyBag.Add(dp.Name, dp);
            string cmdPrefix = string.Concat("OnSet", dp.Name);
            dp.PropertyChanged += dp_PropertyChanged;
            dp.PropertyChanging += dp_PropertyChanging;
        }


        public virtual void RemoveProperty(string name)
        {
            DynamicProperty<T> prop;
            if (this.PropertyBag.TryGetValue(name, out prop))
            {
                this.PropertyBag.Remove(name);
                prop.PropertyChanged -= dp_PropertyChanged;
                prop.PropertyChanging -= dp_PropertyChanging;
            }
        }
        

        public virtual IEnumerable<DynamicFunction<T>> GetFunctions
        {
            get
            {
                OnExtend();
                return FunctionBag.Values;
            }
        }
        protected abstract IEnumerable<DynamicFunction<T>> OnGetFunctions();
        protected virtual void SetFunctions()
        {
            lock (_InvokeByNameHandlers)
            {
                _InvokeByNameHandlers.Clear();
                this.FunctionBag.Clear();

                foreach (DynamicFunction<T> df in this.OnGetFunctions())
                {
                    this.FunctionBag.Add(df.Name, df);
                }

                if (this.FunctionBag.Count > 0)
                {
                    string bodyCS = GetFunctionBody();
                    string references = GetReferences();
                    this.FunctionEvaluator = DynamicFunctionEvaluatorBuilder.Create<T>(this.FunctionBag.Values.ToArray(), this.InstanceName, bodyCS, references);
                }
            }
        }

        public virtual void AddFunction(DynamicFunction<T> function)
        {
            lock (_InvokeByNameHandlers)
            {
                this.FunctionBag.Add(function.Name, function);

                if (this.FunctionBag.Count > 0)
                {
                    string bodyCS = GetFunctionBody();
                    string references = GetReferences();
                    this.FunctionEvaluator = DynamicFunctionEvaluatorBuilder.Create<T>(this.FunctionBag.Values.ToArray(), this.InstanceName, bodyCS, references);
                }
            }
        }

        protected virtual string GetReferences()
        {
            StringBuilder sb = new StringBuilder();
            foreach (DynamicFunction<T> df in this.FunctionBag.Values)
            {
                sb.Append(df.References);
                sb.AppendLine();
            }
            return sb.ToString();
        }

        protected virtual string GetFunctionBody()
        {
            StringBuilder sb = new StringBuilder();
            foreach (DynamicFunction<T> df in this.FunctionBag.Values)
            {
                sb.Append(df.BodyCS);
                sb.AppendLine();
            }
            return sb.ToString();
        }


        protected virtual void dp_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            this.OnPropertyChanged(e.PropertyName);
        }

        protected void dp_PropertyChanging(object sender, PropertyChangingEventArgs e)
        {
            this.OnPropertyChanging(e.PropertyName);
        }

        protected object _backingInstance;
        public object BackingInstance
        {
            get { return OnGetBackingInstance(); }
            set { OnSetBackingInstance(value, false); }
        }



        protected virtual object OnGetBackingInstance() { return _backingInstance; }
        protected virtual void OnSetBackingInstance(object value, bool isCalledFromCtor)
        {
            _backingInstance = value;

            if (_backingInstance is INotifyPropertyChanged)
            {
                ((INotifyPropertyChanged)_backingInstance).PropertyChanged += new PropertyChangedEventHandler(dp_PropertyChanged);
            }

            if (!isCalledFromCtor)
            {
                this.OnPropertyChanged("BackingInstance");
            }
        }

        protected virtual void OnBackingInstanceChanged() { }

        public virtual string InstanceName { get; set; }
        public string InstanceType { get { return OnGetInstanceType(); } }
        protected abstract string OnGetInstanceType();
        public bool IsExtendable { get; protected set; }

        public override DynamicMetaObject GetMetaObject(System.Linq.Expressions.Expression parameter)
        {
            //return base.GetMetaObject(parameter);
            var meta = new DMO(parameter, this);
            return meta;
        }

        private object GetMemberValue(GetMemberBinder binder)
        {
            object result = null;
            if (this.TryGetMember(binder, out result))
            {
                return result;
            }
            else
                throw new Microsoft.CSharp.RuntimeBinder.RuntimeBinderException("Member " + binder.Name + " not found on type "
               + (this.BackingInstance == null ? this.GetType().Name : this.BackingInstance.GetType().Name));
        }

        private object SetMemberValue(SetMemberBinder binder, object value)
        {
            if (!this.TrySetMember(binder, value))
                throw new Microsoft.CSharp.RuntimeBinder.RuntimeBinderException("Member " + binder.Name + " not found on type "
                    + (this.BackingInstance == null ? this.GetType().Name : this.BackingInstance.GetType().Name));
            return null;
        }

        private object Invoke(InvokeBinder binder, object[] args)
        {
            object result = null;
            if (this.TryInvoke(binder, args, out result))
                return result;
            else
                throw new Microsoft.CSharp.RuntimeBinder.RuntimeBinderException("Invoke " + binder.ToString() + " failed on type "
                    + (this.BackingInstance == null ? this.GetType().Name : this.BackingInstance.GetType().Name));
        }
        private object InvokeMember(InvokeMemberBinder binder, object[] args)
        {
            object result = null;
            if (this.TryInvokeMember(binder, args, out result))
                return result;
            else
                throw new Microsoft.CSharp.RuntimeBinder.RuntimeBinderException("Invoke " + binder.Name + " failed on type "
                    + (this.BackingInstance == null ? this.GetType().Name : this.BackingInstance.GetType().Name));
        }

        private class DMO : DynamicMetaObject
        {
            public DMO(System.Linq.Expressions.Expression parameter, Extendable<T> parent)
                : base(parameter, BindingRestrictions.Empty, parent)
            {
                Parent = parent;
            }

            public Extendable<T> Parent { get; private set; }

            public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
            {
                BindingRestrictions restrictions = BindingRestrictions.GetTypeRestriction(Expression, LimitType);

                MethodInfo mi = typeof(Extendable<T>).GetMethod("GetMemberValue", BindingFlags.NonPublic | BindingFlags.Instance);
                Expression[] args = new Expression[]
                {
                    Expression.Constant(binder)
                };
                Expression ret = Expression.Call(Expression.Convert(Expression, typeof(Extendable<T>)), mi, args);
                return new DynamicMetaObject(ret, restrictions);
            }



            public override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value)
            {
                BindingRestrictions restrictions = BindingRestrictions.GetTypeRestriction(Expression, LimitType);

                MethodInfo mi = typeof(Extendable<T>).GetMethod("SetMemberValue", BindingFlags.NonPublic | BindingFlags.Instance);
                Expression[] args = new Expression[]
                {
                    Expression.Constant(binder),
                    Expression.Convert(value.Expression, typeof(object))
                };
                Expression ret = Expression.Call(Expression.Convert(Expression, typeof(Extendable<T>)), mi, args);
                return new DynamicMetaObject(ret, restrictions);
            }

            public override DynamicMetaObject BindInvoke(InvokeBinder binder, DynamicMetaObject[] args)
            {
                BindingRestrictions restrictions = BindingRestrictions.GetTypeRestriction(Expression, LimitType);

                List<Expression> expArgs = new List<System.Linq.Expressions.Expression>();
                for (int i = 0; i < args.Length; i++)
                    expArgs.Add(Expression.Convert(Expression.Convert(args[i].Expression, args[i].LimitType), typeof(object)));

                MethodInfo mi = typeof(Extendable<T>).GetMethod("Invoke", BindingFlags.NonPublic | BindingFlags.Instance);

                Expression ret = Expression.Call(
                    Expression.Convert(Expression, typeof(Extendable<T>)),
                    mi,
                    Expression.Constant(binder),
                    Expression.Convert(Expression.NewArrayInit(typeof(object), expArgs), typeof(object[])));
                return new DynamicMetaObject(ret, restrictions);

            }

            public override DynamicMetaObject BindInvokeMember(InvokeMemberBinder binder, DynamicMetaObject[] args)
            {
                BindingRestrictions restrictions = BindingRestrictions.GetTypeRestriction(Expression, LimitType);

                List<Expression> expArgs = new List<System.Linq.Expressions.Expression>();
                for (int i = 0; i < args.Length; i++)
                    expArgs.Add(Expression.Convert(Expression.Convert(args[i].Expression, args[i].LimitType), typeof(object)));

                MethodInfo mi = typeof(Extendable<T>).GetMethod("InvokeMember", BindingFlags.NonPublic | BindingFlags.Instance);

                Expression ret = Expression.Call(
                    Expression.Convert(Expression, typeof(Extendable<T>)),
                    mi,
                    Expression.Constant(binder),
                    Expression.Convert(Expression.NewArrayInit(typeof(object), expArgs), typeof(object[])));
                return new DynamicMetaObject(ret, restrictions);
            }

        }

        protected Dictionary<string, Func<GetMemberBinder, object>> _memberPointers = new Dictionary<string, Func<GetMemberBinder, object>>();
        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            OnExtend();
            Func<GetMemberBinder, object> member = null;
            result = null;
            lock (_memberPointers)
            {
                if (_memberPointers.ContainsKey(binder.Name))
                {
                    member = _memberPointers[binder.Name];
                }

                if (member == null && this.PropertyBag.ContainsKey(binder.Name))
                {
                    result = this.PropertyBag[binder.Name].Value;
                    member = new Func<GetMemberBinder, object>(delegate (GetMemberBinder b)
                    {
                        object ret = this.PropertyBag[b.Name].Value;
                        return ret;
                    });
                }

                if (member == null && this.FunctionEvaluator != null
                && this.FunctionEvaluator.GetType().GetMember(binder.Name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Length > 0)
                {
                    result = this.FunctionEvaluator;
                    member = new Func<GetMemberBinder, object>(delegate (GetMemberBinder b)
                    {
                        object ret = this.FunctionEvaluator;
                        return ret;
                    });
                }

                if (member == null && this.BackingInstance != null)
                {
                    if (this.BackingInstance is DynamicObject)
                    {
                        if (((DynamicObject)this.BackingInstance).TryGetMember(binder, out result))
                        {
                            member = new Func<GetMemberBinder, object>(delegate (GetMemberBinder b)
                            {
                                object ret;
                                ((DynamicObject)this.BackingInstance).TryGetMember(b, out ret);
                                return ret;
                            });
                        }
                    }
                    else if (TryGetBackingMember(binder, out result))
                    {
                        member = _memberPointers[binder.Name];
                    }
                }

                if (member == null)
                {
                    MemberInfo[] members = this.GetType().GetMember(binder.Name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (members != null && members.Length > 0)
                    {
                        MemberInfo mi = members[0];
                        if (!mi.DeclaringType.Equals(this.GetType()))
                            mi = mi.DeclaringType.GetMember(binder.Name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)[0];
                        member = CreateMemberDelegate(mi, this);
                    }
                }
            }

            if (member == null)
            {
                member = new Func<GetMemberBinder, object>(delegate (GetMemberBinder b)
                {
                    object ret;
                    MemberResolutionFailedCallback(binder.Name, out ret);
                    return ret;
                });
            }

            if (result == null)
                result = member(binder);

            if (!_memberPointers.ContainsKey(binder.Name))
                _memberPointers.Add(binder.Name, member);

            return true;
        }

        protected virtual Func<GetMemberBinder, object> CreateBackingMemberDelegate(MemberInfo mi, object instance)
        {
            if (mi.MemberType == MemberTypes.Property)
            {
                return CreateBackingPropertyDelegate((PropertyInfo)mi, instance);
            }
            else if (mi.MemberType == MemberTypes.Field)
            {
                return CreateBackingFieldDelegate((FieldInfo)mi, instance);
            }
            else if (mi.MemberType == MemberTypes.Method)
            {
                return CreateBackingMethodDelegate((MethodInfo)mi, instance);
            }
            else throw new NotImplementedException("Cannot create member delegate for anything other than Property, Field or Method.");

        }

        protected virtual Func<GetMemberBinder, object> CreateBackingPropertyDelegate(PropertyInfo propertyInfo, object instance)
        {
            if (instance.GetType().IsValueType)
            {
                throw new NotImplementedException();
            }
            else return CreatePropertyDelegate(propertyInfo, instance);
        }

        protected virtual Func<GetMemberBinder, object> CreateBackingFieldDelegate(FieldInfo fieldInfo, object instance)
        {
            if (instance.GetType().IsValueType)
            {
                throw new NotImplementedException();
            }
            else return CreateFieldDelegate(fieldInfo, instance);
        }

        protected virtual Func<GetMemberBinder, object> CreateBackingMethodDelegate(MethodInfo methodInfo, object instance)
        {
            if (instance.GetType().IsValueType)
            {
                throw new NotImplementedException();
            }
            else return CreateMethodDelegate(methodInfo, instance);
        }

        protected virtual Func<GetMemberBinder, object> CreateMemberDelegate(MemberInfo mi, object instance)
        {
            if (mi.MemberType == MemberTypes.Property)
            {
                return CreatePropertyDelegate((PropertyInfo)mi, instance);
            }
            else if (mi.MemberType == MemberTypes.Field)
            {
                return CreateFieldDelegate((FieldInfo)mi, instance);
            }
            else if (mi.MemberType == MemberTypes.Method)
            {
                return CreateMethodDelegate((MethodInfo)mi, instance);
            }
            else throw new NotImplementedException("Cannot create member delegate for anything other than Property, Field or Method.");

        }

        protected virtual Func<GetMemberBinder, object> CreatePropertyDelegate(PropertyInfo pi, object instance)
        {
            Expression instanceExp = Expression.Constant(instance);
            Expression callExp = Expression.Property(instanceExp, pi);
            //if (pi.PropertyType.IsValueType)
            //{
            LambdaExpression lambdaExp = Expression.Lambda<Func<GetMemberBinder, object>>(Expression.Convert(callExp, typeof(object)), Expression.Parameter(typeof(GetMemberBinder)));
            return (Func<GetMemberBinder, object>)lambdaExp.Compile();
            //}
            //else
            //{
            //    // value could be null
            //    Expression resultVarExp = Expression.Variable(pi.PropertyType);
            //    Expression resultExp = Expression.Assign(resultVarExp, callExp);
            //    Expression isNullExp = Expression.Equal(resultExp, Expression.Constant(null));
            //    Expression ifThenElseExp = Expression.IfThenElse(isNullExp, Expression.Constant(null), Expression.Convert(resultExp, typeof(object)));
            //    Expression bodyExp = Expression.Block(
            //        resultVarExp,
            //        resultExp,
            //        ifThenElseExp);
            //    LambdaExpression lambdaExp = Expression.Lambda<Func<GetMemberBinder, object>>(bodyExp, Expression.Parameter(typeof(GetMemberBinder)));
            //    return (Func<GetMemberBinder, object>)lambdaExp.Compile(DebugInfoGenerator.CreatePdbGenerator());
            //}
        }

        protected virtual Func<GetMemberBinder, object> CreateFieldDelegate(FieldInfo fi, object instance)
        {
            Expression instanceExp = Expression.Constant(instance);
            Expression callExp = Expression.Field(instanceExp, fi);
            if (fi.FieldType.IsValueType)
            {
                LambdaExpression lambdaExp = Expression.Lambda<Func<GetMemberBinder, object>>(Expression.Convert(callExp, typeof(object)), Expression.Parameter(typeof(GetMemberBinder)));
                return (Func<GetMemberBinder, object>)lambdaExp.Compile();
            }
            else
            {
                // value could be null
                Expression resultVarExp = Expression.Variable(fi.FieldType);
                Expression resultExp = Expression.Assign(resultVarExp, callExp);
                Expression isNullExp = Expression.Equal(resultExp, Expression.Constant(null));
                Expression ifThenElseExp = Expression.IfThenElse(isNullExp, Expression.Constant(null), Expression.Convert(resultExp, typeof(object)));
                Expression bodyExp = Expression.Block(
                    resultVarExp,
                    resultExp,
                    ifThenElseExp);
                LambdaExpression lambdaExp = Expression.Lambda<Func<GetMemberBinder, object>>(bodyExp, Expression.Parameter(typeof(GetMemberBinder)));
                return (Func<GetMemberBinder, object>)lambdaExp.Compile(DebugInfoGenerator.CreatePdbGenerator());
            }
        }

        protected virtual Func<GetMemberBinder, object> CreateMethodDelegate(MethodInfo mi, object instance)
        {
            Expression instanceExp = Expression.Constant(instance);
            Expression bodyExp = null;
            Expression callExp = Expression.Call(instanceExp, mi);
            if (mi.ReturnType == typeof(void))
            {
                bodyExp = Expression.Block(callExp, Expression.Constant(null));
            }
            else
            {
                bodyExp = Expression.Block(callExp);
            }
            LambdaExpression lambdaExp = Expression.Lambda<Func<GetMemberBinder, object>>(Expression.Convert(bodyExp, typeof(object)), Expression.Parameter(typeof(GetMemberBinder)));
            return (Func<GetMemberBinder, object>)lambdaExp.Compile();
        }

        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            OnExtend();

            if (this.BackingInstance != null)
            {
                if (this.BackingInstance is DynamicObject)
                {
                    if (((DynamicObject)this.BackingInstance).TrySetMember(binder, value))
                        return true;
                }
                else
                {
                    if (TrySetBackingMember(binder.Name, value))
                        return true;
                }
            }
            if (this.PropertyBag.ContainsKey(binder.Name))
            {
                this.PropertyBag[binder.Name].Value = value;
                return true;
            }
            var member = this.GetType().GetMember(binder.Name, BindingFlags.Public | BindingFlags.Instance).SingleOrDefault();
            if (member != null)
            {
                if (member is FieldInfo)
                {
                    ((FieldInfo)member).SetValue(this, value);
                    return true;
                }
                else if (member is PropertyInfo)
                {
                    ((PropertyInfo)member).SetValue(this, value);
                    return true;
                }
            }
            else
            {
                this.AddProperty(binder.Name, value);
                return true;
            }
            return false;
        }

        protected Dictionary<string, Func<T, object>> _InvokeByNameHandlers = new Dictionary<string, Func<T, object>>();
        protected virtual void OnPropertyChangedInvoke(string propertyName)
        {
            propertyName = "On" + propertyName + "Changed";
            this.OnInvokeByName(propertyName);
        }

        protected virtual void OnInvokeByName(string methodName, params object[] args)
        {
            OnExtend();

            Func<T, object> handler = null;
            lock (_InvokeByNameHandlers)
            {
                if ((args == null || args.Length == 0)
                    && _InvokeByNameHandlers.ContainsKey(methodName))
                {
                    handler = _InvokeByNameHandlers[methodName];
                }
                else
                {
                    Type[] typeArgs = new Type[args == null ? 0 : args.Length];
                    Microsoft.CSharp.RuntimeBinder.CSharpArgumentInfo[] argInfos = new Microsoft.CSharp.RuntimeBinder.CSharpArgumentInfo[1 + (args == null ? 0 : args.Length)];
                    argInfos[0] = Microsoft.CSharp.RuntimeBinder.CSharpArgumentInfo.Create(Microsoft.CSharp.RuntimeBinder.CSharpArgumentInfoFlags.None, null);
                    System.Linq.Expressions.Expression[] dynArgs = new System.Linq.Expressions.Expression[1 + (args == null ? 0 : args.Length)];
                    dynArgs[0] = System.Linq.Expressions.Expression.Constant(this);

                    if (args != null)
                    {
                        for (int i = 0; i < args.Length; i++)
                        {
                            if (args[i] == null)
                                typeArgs[i] = typeof(object);
                            else
                                typeArgs[i] = args[i].GetType();
                            argInfos[i + 1] = Microsoft.CSharp.RuntimeBinder.CSharpArgumentInfo.Create(Microsoft.CSharp.RuntimeBinder.CSharpArgumentInfoFlags.None, null);
                            dynArgs[i + 1] = System.Linq.Expressions.Expression.Constant(args[i]);
                        }
                    }
                    CallSiteBinder csb = Microsoft.CSharp.RuntimeBinder.Binder.InvokeMember(
                        Microsoft.CSharp.RuntimeBinder.CSharpBinderFlags.None,
                        methodName,
                        typeArgs,
                        this.GetType(),
                        argInfos);
                    System.Linq.Expressions.Expression exp = System.Linq.Expressions.Expression.Dynamic(
                        csb,
                        typeof(object),
                        dynArgs);
                    System.Linq.Expressions.Expression<Func<T, object>> expr = System.Linq.Expressions.Expression.Lambda<Func<T, object>>(
                        exp,
                        System.Linq.Expressions.Expression.Parameter(typeof(T)));
                    handler = expr.Compile();
                    if (args == null || args.Length == 0)
                        _InvokeByNameHandlers.Add(methodName, handler);
                }
            }
            try
            {
                if (handler != null)
                    handler.DynamicInvoke(this);
            }
            catch (Exception ex)
            {
                if (ex.InnerException == null
                    || !(ex.InnerException is Microsoft.CSharp.RuntimeBinder.RuntimeBinderException))
                {
                    Logger.Log(ex);
                }
                else
                {
                    lock (_InvokeByNameHandlers)
                    {
                        _InvokeByNameHandlers[methodName] = null;
                    }
                }
            }
        }

        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
        {
            OnExtend();

            MethodInfo method = this.GetType().GetMethod(binder.Name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (method != null)
            {
                result = method.Invoke(this, args);
                return true;
            }

            if (this.BackingInstance != null)
            {
                if (this.BackingInstance is DynamicObject)
                {
                    if (((DynamicObject)this.BackingInstance).TryInvokeMember(binder, args, out result))
                        return true;
                }
                else
                {
                    if (TryInvokeBackingMember(binder.Name, args, out result))
                        return true;
                }
            }

            if (this.FunctionEvaluator != null
                && this.FunctionEvaluator.GetType().GetMember(binder.Name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Length > 0)
            {
                result = this.FunctionEvaluator.Execute(binder.Name, args);
                return true;
            }

            DynamicProperty<T> prop;
            if (this.PropertyBag.TryGetValue(binder.Name, out prop) && prop.Value != null && typeof(Delegate).IsAssignableFrom(prop.Value.GetType()))
            {
                result = ((Delegate)prop.Value).DynamicInvoke(args);
                return true;
            }

            result = null;
            return false;
        }

        protected bool TryGetBackingMember(GetMemberBinder binder, out object result)
        {
            Func<GetMemberBinder, object> pointer = null;
            result = null;
            lock (_memberPointers)
            {
                if (_memberPointers.ContainsKey(binder.Name))
                {
                    pointer = _memberPointers[binder.Name];
                }
                else
                {
                    MemberInfo[] members = this.BackingInstance.GetType().GetMember(binder.Name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    if (members != null && members.Length > 0)
                    {
                        MemberInfo mi = members[0]; // pick the first one
                        if (!mi.DeclaringType.Equals(this.BackingInstance.GetType()))
                        {
                            mi = mi.DeclaringType.GetMember(binder.Name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)[0];
                        }
                        pointer = CreateBackingMemberDelegate(mi, this.BackingInstance);
                    }
                    _memberPointers.Add(binder.Name, pointer);
                }
            }
            if (pointer != null)
            {
                if (result == null) result = pointer(binder);
                return true;
            }
            return result != null;
        }

        protected bool TryInvokeBackingMember(string memberName, object[] args, out object result)
        {
            MemberInfo[] members = this.BackingInstance.GetType().GetMember(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            result = null;
            if (members != null && members.Length > 0)
            {
                MemberInfo mi = members[0]; // pick the first one
                if (!mi.DeclaringType.Equals(this.BackingInstance.GetType()))
                {
                    mi = mi.DeclaringType.GetMember(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)[0];
                }
                if (mi is MethodInfo)
                {
                    result = ((MethodInfo)mi).Invoke(this.BackingInstance, args);
                    return true;
                }
            }
            return false;
        }

        protected bool TrySetBackingMember(string memberName, object value)
        {
            MemberInfo[] members = this.BackingInstance.GetType().GetMember(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (members != null && members.Length > 0)
            {
                MemberInfo mi = members[0]; // pick the first one
                if (!mi.DeclaringType.Equals(this.BackingInstance.GetType()))
                {
                    mi = mi.DeclaringType.GetMember(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)[0];
                }
                if (mi is PropertyInfo)
                {
                    ((PropertyInfo)mi).SetValue(this.BackingInstance, value, null);
                    return true;
                }
                else if (mi is FieldInfo)
                {
                    ((FieldInfo)mi).SetValue(this.BackingInstance, value);
                    return true;
                }
            }
            return false;
        }

        public bool TryGetEventMethod(object eventSource, EventInfo eventInfo, string handlerName, out MethodInfo mi, out object target)
        {
            OnExtend();
            target = this;
            mi = this.GetType().GetMethod(handlerName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (mi == null)
            {
                if (this.FunctionEvaluator != null)
                {
                    mi = this.FunctionEvaluator.GetType().GetMethod(handlerName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    target = this.FunctionEvaluator;
                }

                if (mi == null && this.BackingInstance != null)
                {
                    mi = this.FunctionEvaluator.GetType().GetMethod(handlerName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    target = this.BackingInstance;
                }
            }

            if (mi != null)
            {
                ParameterInfo[] parms = mi.GetParameters();
                ParameterInfo[] eParms = eventInfo.EventHandlerType.GetMethod("Invoke").GetParameters();
                if (parms.Length != eParms.Length)
                {
                    mi = null;
                    target = null;
                }
                else
                {
                    for (int i = 0; i < eParms.Length; i++)
                    {
                        if (!(eParms[i].ParameterType.Equals(parms[i].ParameterType)
                            || eParms[i].ParameterType.IsSubclassOf(parms[i].ParameterType)))
                        {
                            mi = null;
                            target = null;
                            break;
                        }
                    }
                }
            }
            return mi != null;
        }
    }
}
