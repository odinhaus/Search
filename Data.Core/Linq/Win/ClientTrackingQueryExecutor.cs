using Data.Core.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Linq.Win
{
    public class ClientTrackingQueryExecutor : TrackingQueryExecutor
    {
        public ClientTrackingQueryExecutor(ITrackingRepository repository, QueryExecutor executor)
            : base(repository, executor)
        { }
    }
}
