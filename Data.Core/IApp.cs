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
    [Model("App", "Shs")]
    public interface IApp : IModel<long>
    {
        string Name { get; set; }
    }

    [TypeConverter(typeof(ModelTypeConverter))]
    [Model("Any", "Shs")]
    public interface IAny : IModel
    {
    }

    [TypeConverter(typeof(ModelTypeConverter))]
    [Model("owns", "Shs")]
    public interface owns : ILink
    {
    }

    [TypeConverter(typeof(ModelTypeConverter))]
    [Model("any", "Shs")]
    public interface any : ILink
    {
    }
}
