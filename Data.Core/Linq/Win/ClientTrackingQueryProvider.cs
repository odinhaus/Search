using Data.Core.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Linq.Win
{
    public class ClientTrackingQueryProvider : Linq.TrackingQueryProvider
    {
        public ClientTrackingQueryProvider(ITrackingRepository respository)
            : base(new ClientQueryProvider(respository))
        {
            this.Cache = respository.QueryCache;
        }

        protected override QueryExecutor CreateExecutor()
        {
            return new ClientTrackingQueryExecutor((ITrackingRepository)this.Repository, ((ICreateExecutor)this.Provider).CreateExecutor());
        }
    }
}
