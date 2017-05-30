using System.Web;
using Autofac.Core.Registration;
using MongoRepository;
using Suffuz.Identity.Models;

[assembly: Microsoft.Owin.OwinStartup(typeof(Suffuz.Identity.Startup))]

namespace Suffuz.Identity
{
    using AspNet.Identity.MongoDB;
    using Autofac;
    using Autofac.Builder;
    using Autofac.Core;
    using Autofac.Integration.WebApi;
    using Common.OWIN;
    using Common.Security;
    using Entities;
    using Microsoft.AspNet.Identity;
    using Microsoft.Owin;
    using Microsoft.Owin.Security;
    using Microsoft.Owin.Security.DataHandler;
    using Microsoft.Owin.Security.DataHandler.Encoder;
    using Microsoft.Owin.Security.DataHandler.Serializer;
    using Microsoft.Owin.Security.DataProtection;
    using Microsoft.Owin.Security.Facebook;
    using Microsoft.Owin.Security.Google;
    using Microsoft.Owin.Security.Infrastructure;
    using Microsoft.Owin.Security.OAuth;
    using Owin;
    using Providers;
    using System;
    using System.Configuration;
    using System.Diagnostics;
    using System.Reflection;
    using System.Web.Http;

    public class Startup
    {
        public static OAuthAuthorizationServerOptions OAuthOptions { get; private set; }
        public static OAuthBearerAuthenticationOptions OAuthBearerOptions { get; private set; }
        public static GoogleOAuth2AuthenticationOptions GoogleAuthOptions { get; private set; }
        public static FacebookAuthenticationOptions FacebookAuthOptions { get; private set; }
        public static IContainer Container { get; private set; }

        public void Configuration(IAppBuilder app)
        {
            app.CreatePerOwinContext<ApplicationUserManager>(ApplicationUserManager.Create);
            app.CreatePerOwinContext<ApplicationIdentityContext>(ApplicationIdentityContext.Create);

            var builder = new ContainerBuilder();

//            builder.Register<ApplicationUserManager>(c => c.Resolve())
            builder.RegisterType<MongoContext>().AsImplementedInterfaces<MongoContext, ConcreteReflectionActivatorData>().SingleInstance();
            builder.RegisterType<AuthRepository>().SingleInstance();
            builder.RegisterType<MongoRepository<Customer>>().AsImplementedInterfaces<IRepository<Customer>,
              ConcreteReflectionActivatorData>().SingleInstance();
            builder.RegisterType<ApplicationIdentityContext>()
                .SingleInstance();
            builder.RegisterType<UserStore<User>>()
                .AsImplementedInterfaces<IUserStore<User>, ConcreteReflectionActivatorData>()
                .SingleInstance();
            builder.RegisterType<RoleStore<Role>>()
                .AsImplementedInterfaces<IRoleStore<Role>, ConcreteReflectionActivatorData>()
                .SingleInstance();
            builder.RegisterType<ApplicationUserManager>()
                            .SingleInstance();
            builder.RegisterType<ApplicationRoleManager>()
                .SingleInstance();
            builder.RegisterType<SimpleAuthorizationServerProvider>()
                .AsImplementedInterfaces<IOAuthAuthorizationServerProvider, ConcreteReflectionActivatorData>().SingleInstance();
            builder.RegisterType<SimpleRefreshTokenProvider>()
                .AsImplementedInterfaces<IAuthenticationTokenProvider, ConcreteReflectionActivatorData>().SingleInstance();
            builder.RegisterApiControllers(typeof(Startup).Assembly);

            var container = builder.Build();
            Container = container;

            app.UseAutofacMiddleware(container);

            var webApiDependencyResolver = new AutofacWebApiDependencyResolver(container);

            var configuration = new HttpConfiguration
            {
                DependencyResolver = webApiDependencyResolver
            };

            ConfigureOAuth(app, container);

            WebApiConfig.Register(configuration);

            app.UseCors(Microsoft.Owin.Cors.CorsOptions.AllowAll);
            app.UseWebApi(configuration);

            app.UseAutofacWebApi(configuration);

            InitializeData(container);

        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is ReflectionTypeLoadException)
            {
                
            }
        }

        private void ConfigureOAuth(IAppBuilder app, IContainer container)
        {
            var OAuthServerOptions = new OAuthAuthorizationServerOptions
            {
                AllowInsecureHttp = true,
                TokenEndpointPath = new PathString("/token"),
                AccessTokenExpireTimeSpan = TimeSpan.FromMinutes(30),
                Provider = container.Resolve<IOAuthAuthorizationServerProvider>(),
                RefreshTokenProvider = container.Resolve<IAuthenticationTokenProvider>(),
                AccessTokenFormat = new SecureDataFormat<AuthenticationTicket>(
                        new TicketSerializer(),
                        new RijndaelTokenProtector(),//new DpapiDataProtectionProvider("SHS").Create("ASP.NET Identity"),
                        TextEncodings.Base64Url)
            };

            OAuthOptions = OAuthServerOptions;
            app.UseExternalSignInCookie(Microsoft.AspNet.Identity.DefaultAuthenticationTypes.ExternalCookie);

            //Configure Google External Login
            GoogleAuthOptions = new GoogleOAuth2AuthenticationOptions()
            {
                ClientId = ConfigurationManager.AppSettings["g_clientId"],
                ClientSecret = ConfigurationManager.AppSettings["g_clientSecret"],
                Provider = new GoogleAuthProvider()
            };
            app.UseGoogleAuthentication(GoogleAuthOptions);

            //Configure Facebook External Login
            FacebookAuthOptions = new FacebookAuthenticationOptions()
            {
                AppId = ConfigurationManager.AppSettings["fb_appId"],
                AppSecret = ConfigurationManager.AppSettings["fb_appSecret"],
                Provider = new FacebookAuthProvider()
            };
            app.UseFacebookAuthentication(FacebookAuthOptions);

            // Token Generation
            app.UseOAuthAuthorizationServer(OAuthServerOptions);
            OAuthBearerOptions = new OAuthBearerAuthenticationOptions()
            {
                AuthenticationMode = AuthenticationMode.Active,
                Provider = new BearerTokenProvider(),
                AccessTokenProvider = new AuthenticationTokenProvider()
                {
                    OnCreate = c => c.SetToken(c.SerializeTicket()),
                    OnReceive = c => c.DeserializeTicket(c.Token)
                },
                AccessTokenFormat = new SecureDataFormat<AuthenticationTicket>(
                        new TicketSerializer(),
                        new RijndaelTokenProtector(), // new DpapiDataProtectionProvider("SHS").Create("ASP.NET Identity"),
                        TextEncodings.Base64Url)
            };
            app.UseOAuthBearerAuthentication(OAuthBearerOptions);
            app.CreatePerOwinContext<OAuthBearerOptionsProvider>(() => new OAuthBearerOptionsProvider(OAuthBearerOptions));
        }

        private void InitializeData(IContainer container)
        {
            var mongoContext = container.Resolve<IMongoContext>();

            if (mongoContext.Clients.Count() == 0)
            {
                mongoContext.Clients.Insert(new Client
                {
                    Id = "Suffuz",
                    Secret = "123@abc".ToBase64SHA256(),
                    Name = "Suffuz",
                    ApplicationType = Models.ApplicationTypes.NativeConfidential,
                    Active = true,
                    RefreshTokenLifeTime = 14400,
                    AllowedOrigin = ConfigurationManager.AppSettings["Allowed_Origin"]
                });
            }
        }
    }
}