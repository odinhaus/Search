using Altus.Suffūz.Serialization.Binary;
using Microsoft.IdentityModel.Claims;
using Common;
using Common.Security;
using Data.Core;
using Data.Core.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Caching;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Security
{
    [TypeConverter(typeof(ModelTypeConverter))]
    [Model("User")]
    public interface IUser : IModel<long>
    {

        [BinarySerializable(10)]
        string Username { get; set; }
        [BinarySerializable(11)]
        string Password { get; set; }
        [BinarySerializable(12)]
        string Title { get; set; }
        [BinarySerializable(13)]
        byte[] Icon { get; set; }
        [BinarySerializable(14)]
        bool IsSystemUser { get; set; }
    }

    public class IUserDefaults
    {
        public const string DefaultIcon = "iVBORw0KGgoAAAANSUhEUgAAACgAAAAoCAYAAAH7+Yj7AAAAGXRFWHRTb2Z0d2FyZQBBZG9iZSBJbWFnZVJlYWR5ccllPAAAAyJpVFh0WE1MOmNvbS5hZG9iZS54bXAAAAAAADw/eHBhY2tldCBiZWdpbj0i77u/IiBpZD0iVzVNME1wQ2VoaUh6cmVTek5UY3prYzlkIj8+IDx4OnhtcG1ldGEgeG1sbnM6eD0iYWRvYmU6bnM6bWV0YS8iIHg6eG1wdGs9IkFkb2JlIFhNUCBDb3JlIDUuMC1jMDYxIDY0LjE0MDk0OSwgMjAxMC8xMi8wNy0xMDo1NzowMSAgICAgICAgIj4gPHJkZjpSREYgeG1sbnM6cmRmPSJodHRwOi8vd3d3LnczLm9yZy8xOTk5LzAyLzIyLXJkZi1zeW50YXgtbnMjIj4gPHJkZjpEZXNjcmlwdGlvbiByZGY6YWJvdXQ9IiIgeG1sbnM6eG1wPSJodHRwOi8vbnMuYWRvYmUuY29tL3hhcC8xLjAvIiB4bWxuczp4bXBNTT0iaHR0cDovL25zLmFkb2JlLmNvbS94YXAvMS4wL21tLyIgeG1sbnM6c3RSZWY9Imh0dHA6Ly9ucy5hZG9iZS5jb20veGFwLzEuMC9zVHlwZS9SZXNvdXJjZVJlZiMiIHhtcDpDcmVhdG9yVG9vbD0iQWRvYmUgUGhvdG9zaG9wIENTNS4xIFdpbmRvd3MiIHhtcE1NOkluc3RhbmNlSUQ9InhtcC5paWQ6MzVENDBFNjg2NEQ0MTFFNkE3OUZCQjg0RTg4MkExNzciIHhtcE1NOkRvY3VtZW50SUQ9InhtcC5kaWQ6MzVENDBFNjk2NEQ0MTFFNkE3OUZCQjg0RTg4MkExNzciPiA8eG1wTU06RGVyaXZlZEZyb20gc3RSZWY6aW5zdGFuY2VJRD0ieG1wLmlpZDozNUQ0MEU2NjY0RDQxMUU2QTc5RkJCODRFODgyQTE3NyIgc3RSZWY6ZG9jdW1lbnRJRD0ieG1wLmRpZDozNUQ0MEU2NzY0RDQxMUU2QTc5RkJCODRFODgyQTE3NyIvPiA8L3JkZjpEZXNjcmlwdGlvbj4gPC9yZGY6UkRGPiA8L3g6eG1wbWV0YT4gPD94cGFja2V0IGVuZD0iciI/Pirkp+0AAAenSURBVHjaYvz//z8D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";
        public const string UnauthorizedUser = "Anonymous";
        public const string UnauthorizedUserTitle = "Built-in Anonymous User";
        public const string AdministrativeUser = "Admin";
        public const string AdministrativeUserTitle = "Built-in Administrative User";
        public const string UserTitle = "Application User";

        public static readonly IPrincipal Administrator;


        static IUserDefaults()
        {
            Administrator = new SuffuzPrincipal(new SuffuzIdentity(AdministrativeUser, "Suffuz", true));
            ((SuffuzIdentity)Administrator.Identity).Claims.Add(new Claim(ClaimTypes.Role, "Admin", ClaimValueTypes.String, AppContext.Name));
            ((SuffuzIdentity)Administrator.Identity).Claims.Add(new Claim(ClaimTypes.NameIdentifier, AdministrativeUser, ClaimValueTypes.String, AppContext.Name));
        }
    }

    public static class SecurityContextEx
    {
        static MemoryCache _cache = new MemoryCache("User_Cache");
        public static IUser ToUser(this SecurityContext ctx)
        {
            var sec = DataAccessSecurityContext.Current.IsEnabled;
            try
            {
                DataAccessSecurityContext.Current.IsEnabled = false;
                IUser user = null;
                if (ctx == null || ctx.CurrentPrincipal == null || ctx.CurrentPrincipal.Identity == null || !ctx.CurrentPrincipal.Identity.IsAuthenticated)
                {
                    user = Model.New<IUser>();
                    user.Username = IUserDefaults.UnauthorizedUser;
                    user.Title = IUserDefaults.UnauthorizedUserTitle;
                }
                else
                {
                    var key = ctx.CurrentPrincipal.Identity.Name;
                    lock (_cache)
                    {
                        if (_cache.Contains(key))
                        {
                            user = _cache[key] as IUser;
                        }
                        else
                        {
                            var uqp = AppContext.Current.Container.GetInstance<IModelQueryProviderBuilder>().CreateQueryProvider<IUser>();
                            user = uqp.Query(string.Format("{0}{{Username ='{1}'}}", ModelTypeManager.GetModelName<IUser>(), ctx.CurrentPrincipal.Identity.Name))
                                .OfType<IUser>()
                                .FirstOrDefault();

                            if (user == null)
                            {
                                user = Model.New<IUser>();
                                user.Username = IUserDefaults.UnauthorizedUser;
                                user.Title = IUserDefaults.UnauthorizedUserTitle;
                                key = user.Username;
                            }

                            _cache.Add(key, user, new CacheItemPolicy() { SlidingExpiration = TimeSpan.FromMinutes(30) });
                        }
                    }
                }
                return user;
            }
            finally
            {
                DataAccessSecurityContext.Current.IsEnabled = sec;
            }
        }
    }
}
