using Arango.Client;
using Common;
using Data.Core;
using Data.Core.Compilation;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.ArangoDB
{
    public class DbContext
    {
        public const string ROOT = "ROOT";

        static DbContext()
        {
            var connectionString = ConfigurationManager.ConnectionStrings[AppContext.Name].ConnectionString;
            var split = connectionString.Split(';');
            var props = new Dictionary<string, string>();
            foreach (var prop in split)
            {
                var parts = prop.Split(':');
                props.Add(parts[0].Trim(), parts[1].Trim());
            }

            ASettings.AddConnection(AppContext.Name,
                props["DatabaseHost"],
                int.Parse(props["DatabasePort"]),
                false,
                props["DatabaseName"],
                props["Username"],
                props["Password"]);
            var db = Create();

            connectionString = ConfigurationManager.ConnectionStrings["Root_Context"].ConnectionString;
            split = connectionString.Split(';');
            props = new Dictionary<string, string>();
            foreach (var prop in split)
            {
                var parts = prop.Split(':');
                props.Add(parts[0].Trim(), parts[1].Trim());
            }

            ASettings.AddConnection(ROOT,
                props["DatabaseHost"],
                int.Parse(props["DatabasePort"]),
                false,
                props["DatabaseName"],
                props["Username"],
                props["Password"]);
        }

        public static ADatabase Create()
        {
            return Create(AppContext.Name);
        }

        public static ADatabase Create(string connectionName)
        {
            return new ADatabase(connectionName);
        }
    }
}
