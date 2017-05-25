using Common.DI;
using Common.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Common.Application
{
    public abstract class ConsoleApplicationContext : ApplicationContextBase
    {
        protected override int RunHosted()
        {
            var ctx = Main(this.Args);
            if (ctx == null)
            {
                System.Windows.Forms.Application.Run();
                return 0;
            }
            else
            {
                System.Windows.Forms.Application.Run(ctx);
                return 0;
            }
        }

        protected override int RunUnhosted()
        {
            return 0;
        }

        protected virtual ApplicationContext Main(params string[] args)
        {
            return null;
        }
    }
}
