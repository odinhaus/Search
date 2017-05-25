using Common.Web.Handlers;
using Suffuz.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace Suffuz.Config
{
    public static class ApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            config.MessageHandlers.Add(new ServiceRoutingHandler());
        }
    }
}
