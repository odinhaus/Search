using Microsoft.Owin.Hosting;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Suffuz
{
    public class Program
    {
        public static string[] Args { get; private set; }

        static void Main(string[] args)
        {
            Args = args;
            var baseUri = ConfigurationManager.AppSettings["BaseUri"];
            Console.WriteLine("Starting web host...");
            WebApp.Start<Startup>(baseUri);
            Console.WriteLine("Web host running at {0}", baseUri);
            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
        }
    }
}
