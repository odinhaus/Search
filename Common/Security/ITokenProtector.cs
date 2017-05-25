using Microsoft.Owin.Security.DataProtection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Security
{
    public interface ITokenProtector : IDataProtector
    {
        string EncryptionKey { get; }
    }
}
