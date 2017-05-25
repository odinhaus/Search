using Altus.Suffūz.Serialization.Binary;
using Data.Core.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Templating
{
    [TypeConverter(typeof(ModelTypeConverter))]
    [Model("DDVTemplate")]
    public interface IDDVTemplate : IModel<long>
    {
        [BinarySerializable(10)]
        string Template { get; set; }
        [BinarySerializable(11)]
        [Searchable]
        string TemplateName { get; set; }
        [BinarySerializable(12)]
        string[] ModelTypes { get; set; }
    }
}
