using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Auditing
{
    public static class AuditSettings
    {
        [ThreadStatic]
        static bool? _isEnabled;
        public static bool IsEnabled
        {
            get { return _isEnabled.HasValue ? _isEnabled.Value : true; }
            set { _isEnabled = value; }
        }
    }
}
