﻿using Data.Core.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Linq.Win
{
    public class ClientQueryMapping : QueryMapping
    {
        public override QueryMapper CreateMapper(QueryTranslator translator)
        {
            return new ClientQueryMapper(this, translator);
        }
    }
}
