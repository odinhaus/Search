using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Web
{
    public partial class ApiUris : IResolveApiUris
    {
        public ApiUris(Environment currentEnvironment)
        {
            this.Environment = currentEnvironment;
        }

        public Environment Environment { get; private set; }

        public virtual string DirectoryUri
        {
            get
            {
                switch(Environment)
                {
                    case Environment.Production:
                    case Environment.Test:
                    case Environment.Demo:
                    case Environment.Beta:
                    case Environment.Development:
                    case Environment.Custom:
                    default:
                        {
                            return ConfigurationManager.AppSettings["IOUri"] + "/Directory";
                        }
                }
            }
        }

        public virtual string FileUri
        {
            get
            {
                switch (Environment)
                {
                    case Environment.Production:
                    case Environment.Test:
                    case Environment.Demo:
                    case Environment.Beta:
                    case Environment.Development:
                    case Environment.Custom:
                    default:
                        {
                            return ConfigurationManager.AppSettings["IOUri"] + "/File";
                        }
                }
            }
        }

        public virtual string DriveUri
        {
            get
            {
                switch (Environment)
                {
                    case Environment.Production:
                    case Environment.Test:
                    case Environment.Demo:
                    case Environment.Beta:
                    case Environment.Development:
                    case Environment.Custom:
                    default:
                        {
                            return ConfigurationManager.AppSettings["IOUri"] + "/Drive";
                        }
                }
            }
        }

        public virtual string SecurityKeyUri
        {
            get
            {
                switch (Environment)
                {
                    case Environment.Production:
                    case Environment.Test:
                    case Environment.Demo:
                    case Environment.Beta:
                    case Environment.Development:
                    case Environment.Custom:
                    default:
                        {
                            return ConfigurationManager.AppSettings["ApiUri"] + "/SecurityKey";
                        }
                }
            }
        }

        public virtual string TokenUri
        {
            get
            {
                switch (Environment)
                {
                    case Environment.Production:
                    case Environment.Test:
                    case Environment.Demo:
                    case Environment.Beta:
                    case Environment.Development:
                    case Environment.Custom:
                    default:
                        {
                            return ConfigurationManager.AppSettings["AuthUri"] + "/Token";
                        }
                }
            }
        }

        public virtual string AuthenticateUri
        {
            get
            {
                switch (Environment)
                {
                    case Environment.Production:
                    case Environment.Test:
                    case Environment.Demo:
                    case Environment.Beta:
                    case Environment.Development:
                    case Environment.Custom:
                    default:
                        {
                            return ConfigurationManager.AppSettings["AuthUri"] + "/Token";
                        }
                }
            }
        }

        public virtual string ChangePasswordUri
        {
            get
            {
                switch (Environment)
                {
                    case Environment.Production:
                    case Environment.Test:
                    case Environment.Demo:
                    case Environment.Beta:
                    case Environment.Development:
                    case Environment.Custom:
                    default:
                        {
                            return ConfigurationManager.AppSettings["AuthUri"] + "/api/Account/ChangePassword";
                        }
                }
            }
        }

        public string Resolve(string provider, string action)
        {
            switch(provider)
            {
                case "Token":
                    return TokenUri;
                case "Authenticate":
                    return AuthenticateUri;
                case "SecurityKey":
                    return SecurityKeyUri;
                case "Drive":
                    return DriveUri + "/" + action;
                case "File":
                    return FileUri + "/" + action;
                case "Directory":
                    return DirectoryUri + "/" + action;
                case "Data":
                    return DirectoryUri + "/" + action;
                case "ChangePassword":
                    return ChangePasswordUri;
            }

            switch (Environment)
            {
                case Environment.Production:
                case Environment.Test:
                case Environment.Demo:
                case Environment.Beta:
                case Environment.Development:
                case Environment.Custom:
                default:
                    {
                        return ConfigurationManager.AppSettings["ApiUri"] + "/" + provider + "/" + action;
                    }
            }
        }
    }
}
