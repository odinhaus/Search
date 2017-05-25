using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Security
{
    public enum KeyType
    {
        CustomerPHIEncryption,
        UserPHIEncryption
    }

    public interface ICustomKey
    {
        string Base64Key { get; set; }
        string Description { get; set; }
        string Type { get; set; }
        uint Version { get; set; }
        string Algorithm { get; set; }
        string DeviceId { get; set; }
        string Username { get; set; }
        string CustomerId { get; set; }
        bool IsCached { get; }
        byte[] Salt { get; set; }
    }
}
