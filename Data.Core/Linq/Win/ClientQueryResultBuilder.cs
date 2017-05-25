using Data.Core.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Linq.Win
{
    public class ClientQueryResultBuilder : QueryResultBuilder
    {
        public ClientQueryResultBuilder(QueryLinguist linguist)
        {
            this.Linguist = linguist;
        }

        public QueryLinguist Linguist { get; private set; }
    }
}
