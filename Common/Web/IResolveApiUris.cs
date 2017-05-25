using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Web
{
    public interface IResolveApiUris
    {
        string DirectoryUri { get; }
        string FileUri { get; }
        string DriveUri { get; }
        string TokenUri { get; }
        string AuthenticateUri { get; }
        string ChangePasswordUri { get; }

        string Resolve(string provider, string action);

    }
}
