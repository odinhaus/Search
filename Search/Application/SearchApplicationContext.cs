using Common.Application;
using Common.DI;
using Suffuz.DI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Suffuz.Application
{
    public class SearchApplicationContext : ConsoleApplicationContext
    {
        public override IContainer CreateContainer()
        {
            return new SearchRegistry().Initialize();
        }

        protected override bool IsAsyncLoaded
        {
            get
            {
                return false;
            }
        }
    }
}
