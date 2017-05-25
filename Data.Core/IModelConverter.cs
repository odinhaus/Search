using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core
{
    public interface IModelConverter
    {
        bool CanConvert(Type sourceType, Type targetType);
        object Convert(object source);
    }
}
