using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Security
{
    public class CustomKey : ICustomKey
    {
        public CustomKey()
        {
        }

        #region IKey implementation
        public string Base64Key { get; set; }
        public string Description { get; set; }
        public string Type { get; set; }
        public uint Version { get; set; }
        public string Algorithm { get; set; }
        public string DeviceId { get; set; }
        public string Username { get; set; }
        public string CustomerId { get; set; }
        public bool IsCached { get; set; }
        public byte[] Salt { get; set; }
        #endregion
    }
}
