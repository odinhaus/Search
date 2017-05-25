using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Linq
{
    public class QueryParameter
    {
        string name;
        Type type;
        StorageType queryType;

        public QueryParameter(string name, Type type, StorageType queryType)
        {
            this.name = name;
            this.type = type;
            this.queryType = queryType;
        }

        public string Name
        {
            get { return this.name; }
        }

        public Type Type
        {
            get { return this.type; }
        }

        public StorageType QueryType
        {
            get { return this.queryType; }
        }
    }
}
