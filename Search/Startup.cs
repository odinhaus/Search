using Microsoft.Owin.FileSystems;
using Microsoft.Owin.StaticFiles;
using Owin;
using Common;
using Suffuz.Config;
using Suffuz.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using Common.Security;
using Data.Core;
using Data.Core.Security;

namespace Suffuz
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            AppContext.Run(createAppContext: false);

            var config = new HttpConfiguration();
            RouteConfig.Register(config);
            ApiConfig.Register(config);
            app.UseWebApi(config);

            SecurityContext.Create<SecurityContextProviderServer>();
            AppContext.Current.Container.GetInstance<IDataContextInitializer>().Initialize();
            DataAccessSecurityContext.Initialize();
            DataAccessSecurityContext.EnforcementType = EnforcementType.Optimistic;
        }
    }
}
