namespace Suffuz.Identity
{
    using AspNet.Identity.MongoDB;
    using Autofac;
    using MongoDB.Driver;
    using System;

    public class ApplicationIdentityContext : IdentityContext, IDisposable
    {
        public ApplicationIdentityContext(IMongoContext mongoContext)
            : this(mongoContext.Users, mongoContext.Roles)
        {
        }

        public ApplicationIdentityContext(MongoCollection users, MongoCollection roles)
            : base(users, roles)
        {
        }

        public static ApplicationIdentityContext Create()
        {
            return new ApplicationIdentityContext(Startup.Container.Resolve<IMongoContext>());
        }

        public void Dispose()
        {
            
        }
    }
}