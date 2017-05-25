using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Scripting
{
    public class ExpressionScope : IDisposable
    {
        [ThreadStatic]
        static Stack<ExpressionScope> _stack = new Stack<ExpressionScope>();
        public ExpressionScope(Type variableType, string name, Expression source, bool isAssignment, object args = null)
        {
            if (_stack == null)
                _stack = new Stack<ExpressionScope>();
            _stack.Push(this);
            this.InstanceType = variableType;
            this.InstanceName = name;
            this.Source = source;
            this.IsAssignment = isAssignment;
            this.Args = args;
        }

        public static ExpressionScope Current
        {
            get
            {
                if (_stack != null && _stack.Count > 0)
                    return _stack.Peek();
                else
                    return null;
            }
        }

        public string InstanceName { get; private set; }
        public Type InstanceType { get; private set; }
        public bool IsAssignment { get; private set; }
        public Expression Source { get; private set; }

        public object Args { get; private set; }

        public void Dispose()
        {
            _stack.Pop();
        }
    }
}
