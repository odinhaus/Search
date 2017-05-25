using Common;
using Data.Core.Compilation;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Dynamic
{
    public static class DynamicFunctionEvaluatorBuilder
    {
        static Dictionary<string, Type> _evaluators = new Dictionary<string, Type>();
        public static IDynamicFunctionEvaluator Create<T>(DynamicFunction<T>[] instance, string instanceName, string bodyCS, string references) where T : IDynamicMetaObjectProvider
        {
            instanceName = instanceName.StripSpecial("_");
            string key = instanceName;
            lock (_evaluators)
            {
                if (_evaluators.ContainsKey(key))
                {
                    return Activator.CreateInstance(_evaluators[key], instance[0].TargetInstance, instanceName) as IDynamicFunctionEvaluator;
                }
                else
                {
                    string template = _Template.Replace("@InstanceName", instanceName);
                    template = template.Replace("@Type", typeof(T).FullName);
                    template = template.Replace("@Body", bodyCS.Replace("this.", "((dynamic)this)."));

                    bool hasErrors;
                    CompilerErrorCollection errors;
                    Type evaluatorType = CSharpCompiler.Compile(template,
                        instanceName + "_" + typeof(IDynamicFunctionEvaluator).Name,
                        AppContext.GetEnvironmentVariable("TempDir", "Temp").ToString(),
                        string.IsNullOrEmpty(references) ? null : references.Split(';'),
                        out hasErrors,
                        out errors);

                    if (hasErrors)
                    {
                        throw (new InvalidProgramException(errors.ToErrorString()));
                    }
                    else
                    {
                        _evaluators.Add(key, evaluatorType);
                        return Activator.CreateInstance(evaluatorType, instance[0].TargetInstance, instanceName) as IDynamicFunctionEvaluator;
                    }
                }
            }
        }

        private const string _Template = @"using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Dynamic;
using Altus;
using Altus.Core.Component;

namespace Altus.Core.Dynamic
{
    public class @InstanceName_IDynamicFunctionEvaluator : DynamicObject, IDynamicFunctionEvaluator
    {
        public @InstanceName_IDynamicFunctionEvaluator(dynamic instance, string instanceName)
        {
            this.InstanceName = instanceName;
            this.Instance = instance;
        }

        public string InstanceName { get; private set; }
        public dynamic Instance { get; private set; }

        public object Execute(string methodName, object[] args)
        {
            MethodInfo[] methods = this.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(mi => mi.Name.Equals(methodName, StringComparison.InvariantCultureIgnoreCase)).ToArray();
            
            if (methods != null && methods.Length > 0)
            {
                MethodInfo method = MatchMethodToArgs(methods, args); ;

                if (method != null)
                    return method.Invoke(this, args);
            }
            
            throw (new NotImplementedException(""The function "" + methodName + "" is not implemented on instance "" + this.InstanceName));
        }

        private MethodInfo MatchMethodToArgs(MethodInfo[] methods, object[] args)
        {
            if (args == null) args = new object[0];

            foreach(MethodInfo mi in methods)
            {
                ParameterInfo[] parms = mi.GetParameters();
                if (parms.Length == args.Length)
                {
                    for (int i = 0; i < parms.Length; i++)
                    {
                        if (!parms[i].ParameterType.Equals(typeof(object))
                            && !(parms[i].ParameterType.Equals(args[i].GetType())
                            || args[i].GetType().IsSubclassOf(parms[i].ParameterType)))
                        {
                            break;
                        }
                    }
                    return mi;
                }
            }

            return null;
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            return this.Instance.TryGetMember(binder, out result);
        }

        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            return this.Instance.TrySetMember(binder, value);
        }

        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
        {
            return this.Instance.TryInvokeMember(binder, args, out result);
        }

        @Body
    }
}";
    }
}
