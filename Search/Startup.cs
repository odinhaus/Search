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
using Microsoft.Owin.Security.OAuth;
using Microsoft.Owin.Security.Infrastructure;
using Microsoft.Owin.Security.DataHandler;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.DataHandler.Serializer;
using Microsoft.Owin.Security.DataHandler.Encoder;
using Common.OWIN;

namespace Suffuz
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            AppContext.Run(Program.Args, createAppContext: false);

            var options = new OAuthBearerAuthenticationOptions()
            {
                AuthenticationMode = Microsoft.Owin.Security.AuthenticationMode.Active,
                Provider = new BearerTokenProvider(),
                AccessTokenProvider = new AuthenticationTokenProvider()
                {
                    OnCreate = c => c.SetToken(c.SerializeTicket()),
                    OnReceive = c => c.DeserializeTicket(c.Token)
                },
                AccessTokenFormat = new SecureDataFormat<AuthenticationTicket>(
                        new TicketSerializer(),
                        new RijndaelTokenProtector(), //new DpapiDataProtectionProvider("DF").Create("ASP.NET Identity"),
                        TextEncodings.Base64Url)
            };

            app.CreatePerOwinContext(() => new OAuthBearerOptionsProvider(options));
            app.CreatePerOwinContext(() => new AppBuilderProvider(app));

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
