﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Linq
{
    public static class ReadOnlyEx
    {
        public static ReadOnlyCollection<T> ToReadOnly<T>(this IEnumerable<T> collection)
        {
            var roc = collection as ReadOnlyCollection<T>;
            if (roc == null)
            {
                if (collection == null)
                {
                    roc = EmptyReadOnlyCollection<T>.Empty;
                }
                else
                {
                    roc = new List<T>(collection).AsReadOnly();
                }
            }
            return roc;
        }

        class EmptyReadOnlyCollection<T>
        {
            internal static readonly ReadOnlyCollection<T> Empty = new List<T>().AsReadOnly();
        }
    }
}
