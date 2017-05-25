using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Threading
{
    public static class Thread
    {
        public static System.Threading.Thread Run(Action action)
        {
            var t = new System.Threading.Thread(new System.Threading.ThreadStart(action));
            t.IsBackground = true;
            t.Start();
            return t;
        }
    }
}
