using Microsoft.IdentityModel.Claims;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Security
{
    public class SuffuzImpersonationPrincipal : SuffuzPrincipal, IDisposable
    {
        [ThreadStatic]
        static List<SuffuzImpersonationPrincipal> _impersonationStack = new List<SuffuzImpersonationPrincipal>();

        internal static SuffuzImpersonationPrincipal Impersonate(SuffuzPrincipal principalToImpersonate)
        {
            var imp = new SuffuzImpersonationPrincipal(principalToImpersonate);
            try
            {
                _impersonationStack.Insert(0, imp);
            }
            catch (NullReferenceException)
            {
                _impersonationStack = new List<SuffuzImpersonationPrincipal>();
                _impersonationStack.Insert(0, imp);
            }
            return imp;
        }

        private SuffuzImpersonationPrincipal(SuffuzPrincipal principalToImpersonate) : base((IClaimsIdentity)principalToImpersonate.Identity)
        {
        }

        internal static SuffuzPrincipal Current
        {
            get
            {
                try
                {
                    return _impersonationStack == null || _impersonationStack.Count == 0 ? null : _impersonationStack[0];
                }
                catch (NullReferenceException)
                {
                    _impersonationStack = new List<SuffuzImpersonationPrincipal>();
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
