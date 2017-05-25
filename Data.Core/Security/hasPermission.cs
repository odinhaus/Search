using Data.Core.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Security
{
    [TypeConverter(typeof(ModelTypeConverter))]
    [Model("hasPermission", "Security")]
    public interface hasPermission : ILink<long>
    {
    }
}
