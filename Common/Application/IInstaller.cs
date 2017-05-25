using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Application
{
    public interface IInstaller : IInitialize
    {
        /// <summary>
        /// Gets a value indicating whether installer ran sucessfully for all target app contexts
        /// </summary>
        bool IsInstalled { get; }
        /// <summary>
        /// Install for each of the target appNames.  If appNames is null or the length is zero, 
        /// installer should run for all current apps in the Instance.
        /// </summary>
        /// <param name="appNames"></param>
        void Install(params string[] appNames);
    }
}
