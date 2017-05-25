using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Linq
{
    public abstract class QueryLanguage
    {
        public abstract QueryTypeSystem TypeSystem { get; }
        public abstract QueryLinguist CreateLinguist(QueryTranslator translator);
    }
}
