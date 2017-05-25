using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Dynamic
{
    public interface IDynamicPropertyEvaluator : INotifyPropertyChanged
    {
        dynamic Instance { get; }
        Func<object> Gettor { get; }
        Action<Object> Settor { get; }
    }
}
