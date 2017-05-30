namespace Suffuz.Identity
{
    using AspNet.Identity.MongoDB;
    using Common.Security;
    using Entities;
    using Microsoft.AspNet.Identity;
    using Microsoft.AspNet.Identity.Owin;
    using Microsoft.Owin;
    using Microsoft.Owin.Security.DataProtection;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public class ApplicationUserManager : UserManager<User>
    {
        public ApplicationUserManager(ApplicationIdentityContext identityContext)
            : base(new UserStore<User>(identityContext))
        {
        }
        public static ApplicationUserManager Create(IdentityFactoryOptions<ApplicationUserManager> options, IOwinContext context)
        {
            var manager = new ApplicationUserManager(ApplicationIdentityContext.Create());
            // Configure validation logic for usernames
            manager.UserValidator = new ApplicationUserValidator(manager)
            {
                AllowOnlyAlphanumericUserNames = false,
                RequireUniqueEmail = true
            };
            // Configure validation logic for passwords
            manager.PasswordValidator = new PasswordValidator
            {
                RequiredLength = 6,
                RequireNonLetterOrDigit = true,
                RequireDigit = true,
                RequireLowercase = true,
                RequireUppercase = true,
            };

            manager.UserTokenProvider = new DataProtectorTokenProvider<User>(new RijndaelTokenProtector());// new DataProtectorTokenProvider<User>(new DpapiDataProtectionProvider("SHS").Create("ASP.NET Identity"));

            // TODO: Email service for notifications
            //manager.EmailService = new EmailService();

            return manager;
        }



    }

    public class ApplicationUserValidator : UserValidator<User>
    {
        private UserManager<User, string> UserManager;

        public ApplicationUserValidator(UserManager<User, string> manager) : base(manager)
        {
            this.UserManager = manager;
            this.ValidateHandle = false;
        }

        public override async Task<IdentityResult> ValidateAsync(User item)
        {
            var baseValidation = await base.ValidateAsync(item);
            var errors = new List<string>(baseValidation.Errors ?? new string[0]);
            if (errors.Count > 0 && errors[0].StartsWith("Name"))
            {
                errors.RemoveAt(0);
            }
            return baseValidation;
        }

        public bool ValidateHandle { get; set; }
    }
}