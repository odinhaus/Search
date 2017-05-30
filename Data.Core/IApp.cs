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
    [Model("App")]
    public interface IApp : IModel<long>
    {
        string Name { get; set; }
    }

    [TypeConverter(typeof(ModelTypeConverter))]
    [Model("Any")]
    public interface IAny : IModel
    {
    }

    [TypeConverter(typeof(ModelTypeConverter))]
    [Model("owns")]
    public interface owns : ILink
    {
    }

    [TypeConverter(typeof(ModelTypeConverter))]
    [Model("any")]
    public interface any : ILink
    {
    }
}
