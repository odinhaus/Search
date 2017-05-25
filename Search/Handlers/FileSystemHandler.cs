using Common.Web.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Threading;
using Microsoft.Owin.FileSystems;
using Common.IO;
using System.IO;

namespace Suffuz.Handlers
{
    public class FileSystemHandler : IDelegatingHandler
    {
        public FileSystemHandler(string root)
        {
            this.Root = root;
        }

        public string Root { get; private set; }

        public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                var fileSystem = new FileSystem(this.Root);
                IFileInfo fi;
                if (fileSystem.TryGetFileInfo(request.RequestUri.LocalPath, out fi))
                {
                    var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
                    using (var stream = fi.CreateReadStream())
                    {
                        response.Content = new ByteArrayContent(StreamHelper.GetBytes(stream));
                        response.Content.Headers.Add("Content-Type", GetContentType(fi));
                    }
                    return response;
                }
                else
                {
                    return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
                }
            });
        }

        private string GetContentType(IFileInfo fi)
        {
            var ext = Path.GetExtension(fi.PhysicalPath);
            switch(ext.ToLower())
            {
                case ".html":
                case ".htm":
                    return "text/html";
                case ".png":
                    return "image/png";
                case ".jpg":
                case ".jpeg":
                    return "image/jpeg";
                case ".gif":
                    return "image/gif";
                case ".pdf":
                    return "application/pdf";
                case ".json":
                    return "application/json";
                case ".txt":
                default:
                    return "text";
            }
        }
    }
}
