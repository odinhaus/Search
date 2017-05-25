using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Licensing
{
    public interface ILicensedPlugin
    {
        void ApplyLicensing(ILicense[] licenses, params string[] args);
        bool IsLicensed(object plugin);
    }
}
