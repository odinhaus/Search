using System;
using System.Linq.Expressions;
using Data.Core.Linq;
using Common.Serialization;
using Common.Serialization.Binary;
using Altus.Suffūz.Serialization;

namespace Data.Core.Linq.Win
{
    internal class ClientQueryLinguist : QueryLinguist
    {
        public ClientQueryLinguist(QueryLanguage language, QueryTranslator translator) : base(language, translator)
        {
        }

        public override string Format(Expression expression)
        {
            return System.Convert.ToBase64String(((IBinarySerializable)expression).ToBytes());
        }
    }
}