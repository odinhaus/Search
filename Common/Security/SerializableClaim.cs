using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Security
{
    public class SerializableClaim
    {
        public string Type { get; set; }
        public string Issuer { get; set; }
        public string OriginalIssuer { get; set; }
        public string Value { get; set; }
        public string ValueType { get; set; }
    }
}
