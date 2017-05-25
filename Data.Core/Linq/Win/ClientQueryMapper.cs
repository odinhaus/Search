using System;
using Data.Core.Linq;

namespace Data.Core.Linq.Win
{
    internal class ClientQueryMapper : QueryMapper
    {
        private QueryMapping mapping;
        private QueryTranslator translator;

        public ClientQueryMapper(QueryMapping mapping, QueryTranslator translator)
        {
            this.translator = translator;
            this.mapping = mapping;
        }

        public override QueryMapping Mapping
        {
            get
            {
                return mapping;
            }
        }

        public override QueryTranslator Translator
        {
            get
            {
                return translator;
            }
        }
    }
}