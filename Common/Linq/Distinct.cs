using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Linq
{
    public static partial class Enumerable
    {
        public static IEnumerable<T> Distinct<T>(this IEnumerable<T> source, EqualityComparer<T> comparer, ValueSelector<T> selector)
        {
            var distinct = new List<T>();
            var list = source.ToList();
            var count = list.Count;
            T item, innerItem; 

            for(int outer = 0; outer < count; outer++)
            {
                item = list[outer];
                for(int inner = outer + 1; inner < count; inner ++)
                {
                    innerItem = list[inner];
                    if (comparer(item, innerItem))
                    {
                        item = selector(item, innerItem);
                        list.RemoveAt(inner);
                        count--;
                    }
                }

                distinct.Add(item);
            }

            return list;
        }
    }

    public delegate bool EqualityComparer<T>(T v1, T v2);
    public delegate T ValueSelector<T>(T value1, T value2);
}
