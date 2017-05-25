using Data.Core.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Linq.Win
{
    public class ClientQueryLanguage : QueryLanguage
    {
        ClientTypeSystem _typeSystem;

        public ClientQueryLanguage()
        {
            _typeSystem = new ClientTypeSystem();
        }

        public override QueryTypeSystem TypeSystem
        {
            get
            {
                return _typeSystem;
            }
        }

        public override QueryLinguist CreateLinguist(QueryTranslator translator)
        {
            return new ClientQueryLinguist(this, translator);
        }
    }
}
