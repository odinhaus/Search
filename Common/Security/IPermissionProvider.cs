using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace Common.Security
{
    public interface IPermissionProvider
    {
        /// <summary>
        /// Gets the list of Permissions associated with the current SecurityContext.Current.CurrentPrincipal
        /// </summary>
        /// <returns></returns>
        IEnumerable<IPermission> GetPermissions();
    }
}
