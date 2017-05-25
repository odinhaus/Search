using Microsoft.IdentityModel.Claims;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Security
{
    public class SHSImpersonationPrincipal : SHSPrincipal, IDisposable
    {
        [ThreadStatic]
        static List<SHSImpersonationPrincipal> _impersonationStack = new List<SHSImpersonationPrincipal>();

        internal static SHSImpersonationPrincipal Impersonate(SHSPrincipal principalToImpersonate)
        {
            var imp = new SHSImpersonationPrincipal(principalToImpersonate);
            try
            {
                _impersonationStack.Insert(0, imp);
            }
            catch (NullReferenceException)
            {
                _impersonationStack = new List<SHSImpersonationPrincipal>();
                _impersonationStack.Insert(0, imp);
            }
            return imp;
        }

        private SHSImpersonationPrincipal(SHSPrincipal principalToImpersonate) : base((IClaimsIdentity)principalToImpersonate.Identity)
        {
        }

        internal static SHSPrincipal Current
        {
            get
            {
                try
                {
                    return _impersonationStack == null || _impersonationStack.Count == 0 ? null : _impersonationStack[0];
                }
                catch (NullReferenceException)
                {
                    _impersonationStack = new List<SHSImpersonationPrincipal>();
                    return null;
                }
            }
        }


        public void Dispose()
        {
            _impersonationStack.Remove(this);
        }
    }
}
