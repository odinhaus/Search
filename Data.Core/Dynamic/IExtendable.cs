using System.Collections.Generic;
using System.ComponentModel;
using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;

namespace Data.Core.Dynamic
{
    public interface IExtendable<T> : IExtendable where T : IDynamicMetaObjectProvider
    {
        void AddFunction(DynamicFunction<T> function);
        void AddProperty(DynamicProperty<T> dp);

        IEnumerable<DynamicFunction<T>> GetFunctions { get; }
        IEnumerable<DynamicProperty<T>> GetProperties { get; }
    }

    public interface IExtendable
    {
        IEnumerable<string> Aliases { get; }
        object BackingInstance { get; set; }
        string InstanceType { get; }
        bool IsExtendable { get; }
        string InstanceName { get; set; }

        event PropertyChangedEventHandler PropertyChanged;

        void AddProperty(string name, object scalarValue);
        void AddProperty<U>(string name);
        DynamicMetaObject GetMetaObject(Expression parameter);
        bool HasAlias(string name);
        bool HasMethod(string name);
        bool HasProperty(string name);
        bool TryGetEventMethod(object eventSource, EventInfo eventInfo, string handlerName, out MethodInfo mi, out object target);
        bool TryGetMember(GetMemberBinder binder, out object result);
        bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result);
        bool TrySetMember(SetMemberBinder binder, object value);
    }
}