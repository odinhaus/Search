using Arango.Client;
using Data.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.ArangoDB
{
    public static class ArangoExtensions
    {
        public static List<Dictionary<string, object>> AqlTransacted(this ADatabase database, string aql, string[] writeCollections = null, string[] readCollections = null)
        {
            var trans = database.Transaction;
            if (writeCollections != null)
            {
                foreach(var cname in writeCollections)
                {
                    trans = trans.WriteCollection(cname.Replace(".", "_"));
                }
            }
            if (readCollections != null)
            {
                foreach (var cname in readCollections)
                {
                    trans = trans.ReadCollection(cname.Replace(".", "_"));
                }
            }
            aql = aql.Replace("'", @"\'").Replace("\t", " ").Replace("\n", " ").Replace("\r","");
            var len = aql.Length;
            do
            {
                len = aql.Length;
                aql = aql.Replace("  ", " ");
            } while (aql.Length != len);
            var action = string.Format("function () {{ var db = require('internal').db; return db._query('{0}').toArray(); }}", aql).Replace(System.Environment.NewLine, " ");
            return trans.Execute<List<Dictionary<string, object>>>(action).Value;
        }
    }
}
