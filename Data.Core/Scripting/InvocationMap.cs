using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Scripting
{
    public class InvocationMap
    {
        Dictionary<string, Delegate> _members = new Dictionary<string, Delegate>();
        public void Add(string name, Delegate del)
        {
            lock (_members)
                _members.Add(name, del);
        }

        public bool TryGet(string name, out Delegate del)
        {
            lock (_members)
                return _members.TryGetValue(name, out del);
        }
    }
}
