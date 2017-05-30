using Owin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.Owin.Hosting;
using Microsoft.Owin.FileSystems;
using Microsoft.Owin.StaticFiles;

namespace Shs.Auth.Web
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            var root = System.IO.Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).Parent.FullName;
            var fileSystem = new PhysicalFileSystem(root);
            var options = new FileServerOptions()
            {
                EnableDirectoryBrowsing = true,
                EnableDefaultFiles = true,
                FileSystem = fileSystem
            };
            app.UseFileServer(options);
        }
    }
}