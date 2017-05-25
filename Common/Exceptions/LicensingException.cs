using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Exceptions
{
    public class LicensingException : Exception
    {
        public LicensingException() : base() { }
        public LicensingException(string message) : base(message) { }
    }
}
