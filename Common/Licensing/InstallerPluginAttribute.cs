using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Licensing
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class InstallerPluginAttribute : Attribute
    {
        /// <summary>
        /// Decorate IInstaller types and provide a list of target appNames that the 
        /// installer should run for.  Passing no app names will run the installer 
        /// for all apps.
        /// </summary>
        /// <param name="appNames"></param>
        public InstallerPluginAttribute(params string[] appNames)
        {
            Apps = appNames;
        }

        public string[] Apps { get; private set; }
    }
}
