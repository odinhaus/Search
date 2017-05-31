using Microsoft.Owin.Hosting;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Web;

namespace Suffuz.Identity
{
    public static class Program
    {
        public static string[] Args { get; private set; }

        public static void Main(params string[] args)
        {
            try
            {
                Args = args;
                var baseUri = ConfigurationManager.AppSettings["Allowed_Origin"];
                Console.WriteLine("Starting web host...");
                WebApp.Start<Startup>(baseUri);
                Console.WriteLine("Web host running at {0}", baseUri);
                Console.WriteLine("Press any key to exit");
                Console.ReadKey();
            }
            catch(Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }
    }
}