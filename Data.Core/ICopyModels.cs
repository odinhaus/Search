using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core
{
    public interface ICopyModels
    {
        IModel Clone(IModel source);
        void Copy(IModel source, IModel destination);
    }
}
