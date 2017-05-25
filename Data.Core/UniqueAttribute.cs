using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class UniqueAttribute : Attribute
    {
        public UniqueAttribute()
        {
            IsUnique = true;
        }
        public bool IsUnique { get; set; }
    }
}
