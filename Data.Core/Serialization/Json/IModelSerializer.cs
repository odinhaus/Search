using Altus.Suffūz.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Serialization.Json
{
    public interface IModelSerializer : ISerializer<IModel>
    {
        //Type ModelType { get; set; }
    }
}
