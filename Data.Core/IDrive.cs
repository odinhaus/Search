using Common.Security;
using Data.Core.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core
{
    [TypeConverter(typeof(ModelTypeConverter))]
    [Model("Drive", "Shs")]
    public interface IDrive : IModel<long>
    {
        string CustomerId { get; set; }
        long AvailableFreeSpace { get; set; }
        long TotalSize { get; set; }
        long UsedSpace { get; set; }
    }
}
