using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Web
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public class OverrideAttribute : Attribute
    {
        public OverrideAttribute(Type iOverrideType)
        {
            if (!iOverrideType.Implements<IOverride>())
                throw new InvalidOperationException("Type must implement IOverride.");
            this.OverrideType = iOverrideType;
        }

        public Type OverrideType { get; private set; }
        public IOverride CreateOverride()
        {
            return Activator.CreateInstance(OverrideType) as IOverride;
        }
    }
}
