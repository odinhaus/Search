using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Web
{
    public interface IOverride
    {
        Expression CreateCall(Expression provider, MethodInfo method, Expression argumentsArray);
        Argument[] ArgumentTypes { get; }
        Type ReturnType { get; }
    }

    public class Argument
    {
        public Argument(string name, Type type)
        {
            this.Name = name;
            this.Type = type;
        }
        public string Name { get; private set; }
        public Type Type { get; private set; }
    }
}
