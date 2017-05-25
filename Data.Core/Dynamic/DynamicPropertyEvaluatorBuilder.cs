using Common;
using Data.Core.Compilation;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Data.Core.Dynamic
{
    public static class DynamicPropertyEvaluatorBuilder
    {
        static Dictionary<string, Type> _evaluators = new Dictionary<string, Type>();
        public static IDynamicPropertyEvaluator Create(dynamic instance, string instanceName, string propertyName, string gettorCS, string settorCS, string bodyCS, string references)
        {
            string key = instanceName.Replace(".", "_") + "_" + propertyName;
            lock (_evaluators)
            {
                if (_evaluators.ContainsKey(key))
                {
                    return Activator.CreateInstance(_evaluators[key], instance) as IDynamicPropertyEvaluator;
                }
                else
                {
                    gettorCS = gettorCS.Replace("this", "this.Instance");
                    settorCS = settorCS.Replace("this", "this.Instance");
                    bodyCS = bodyCS.Replace("this", "this.Instance");
                    string template = _Template.Replace("@InstanceName", instanceName.Replace(":", "__").Replace(".", "_"));
                    template = template.Replace("@PropertyName", propertyName.Trim());
                    template = template.Replace("@Gettor", string.IsNullOrEmpty(gettorCS) ? "return null;" : gettorCS);
                    template = template.Replace("@Settor", string.IsNullOrEmpty(settorCS) ? "" : settorCS);
                    template = template.Replace("@Body", bodyCS);

                    StringBuilder instProps = new StringBuilder();
                    Regex r = new Regex(@"this\.Instance\.(?<prop>\w+)", RegexOptions.IgnoreCase);
                    Match m = r.Match(gettorCS);
                    while (m.Success)
                    {
                        instProps.Append(m.Groups["prop"].Value);
                        instProps.Append("||");
                        m = m.NextMatch();
                    }

                    m = r.Match(settorCS);
                    while (m.Success)
                    {
                        instProps.Append(m.Groups["prop"].Value);
                        instProps.Append("||");
                        m = m.NextMatch();
                    }

                    template = template.Replace("@InstanceProps", "\"" + instProps.ToString() + "\"");

                    bool hasErrors;
                    CompilerErrorCollection errors;
                    Type evaluatorType = CSharpCompiler.Compile(template,
                        instanceName.Replace(":", "__").Replace(".", "_") + "_" + propertyName.Trim() + "_" + typeof(IDynamicPropertyEvaluator).Name,
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
                        return Activator.CreateInstance(evaluatorType, instance) as IDynamicPropertyEvaluator;
                    }
                }
            }
        }

        private const string _Template = @"using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace Altus.dynamic
{
    public class @InstanceName_@PropertyName_IDynamicPropertyEvaluator : IDynamicPropertyEvaluator
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public @InstanceName_@PropertyName_IDynamicPropertyEvaluator(dynamic instance) 
        {
            this.Gettor = new Func<object>(this._Gettor);
            this.Settor = new Action<object>(this._Settor);
            this.Instance = instance;
            if (instance is INotifyPropertyChanged)
            {
                ((INotifyPropertyChanged)instance).PropertyChanged += OnInstancePropertyChanged;
            }
        }

        public Func<object> Gettor
        {
            get;
            private set;
        }

        public Action<object> Settor
        {
            get;
            private set;
        }

        public dynamic Instance { get; private set; }

        @Body

        private object _Gettor()
        {
            @Gettor
        }

        private void _Settor(object value)
        {
            @Settor
            OnPropertyChanged(""@PropertyName"");
        }

        protected void OnPropertyChanged(string name)
        {
            if (this.PropertyChanged != null)
            {
                this.PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        protected void OnInstancePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (@InstanceProps.Contains(e.PropertyName + ""||""))
                OnPropertyChanged(""@PropertyName"");
        }
    }
}";
    }
}
