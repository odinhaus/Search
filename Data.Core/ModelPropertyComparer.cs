using Data.Core.Auditing;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core
{
    public static class ModelPropertyComparer
    {
        public static IEnumerable<AuditedChange> CompareValues<T>(T source, T target, string propertyName)
        {
            if (source == null && target != null)
            {
                return new AuditedChange[] { new AuditedChange() { PropertyName = propertyName, ChangeType = AuditChangeType.Removed, ItemIndex = 0, Value = source } };
            }
            else if (source != null && !source.Equals(target))
            {
                return new AuditedChange[] { new AuditedChange() { PropertyName = propertyName, ChangeType = AuditChangeType.NewOrModified, ItemIndex = 0, Value = source } };
            }
            return new AuditedChange[0];
        }

        public static IEnumerable<AuditedChange> Compare(IEnumerable enSource, IEnumerable enTarget, string propertyName)
        {
            List<AuditedChange> changes = new List<AuditedChange>();
            // look for adds/updates
            var source = enSource?.GetEnumerator() ?? null;
            var target = enTarget?.GetEnumerator() ?? null;
            var index = 0;

            while (source != null && source.MoveNext())
            {
                var item = source.Current;
                if (item is IModel)
                {
                    var key = ((IModel)item).GetKey();
                    var found = FirstOrDefault<IModel>(target, a => a.GetKey()?.Equals(key) ?? false);
                    if (found == null)
                    {
                        changes.Add(new AuditedChange() { PropertyName = propertyName, ChangeType = AuditChangeType.Added, Value = item, ItemIndex = index });
                    }
                    else
                    {
                        changes.AddRange(((IModel)item).Compare(found, propertyName + "."));
                    }
                }
                else
                {
                    var found = FirstOrDefault<object>(target, a => a.Equals(item));
                    if (found == null)
                    {
                        changes.Add(new AuditedChange() { PropertyName = propertyName, ChangeType = AuditChangeType.Added, Value = item, ItemIndex = index });
                    }
                }
                index++;
                target?.Reset();
            }

            source?.Reset();
            target?.Reset();

            while (target != null && target.MoveNext())
            {
                var item = target.Current;
                if (item is IModel)
                {
                    var key = ((IModel)item).GetKey();
                    if (!Any<IModel>(source, a => a.GetKey()?.Equals(key) ?? false))
                    {
                        changes.Add(new AuditedChange() { PropertyName = propertyName, ChangeType = AuditChangeType.Removed, ItemIndex = -1, Value = item });
                    }
                }
                else
                {
                    if (!Any<object>(source, a => a.Equals(item)))
                    {
                        changes.Add(new AuditedChange() { PropertyName = propertyName, ChangeType = AuditChangeType.Removed, ItemIndex = -1, Value = item });
                    }
                }
                source?.Reset();
            }

            return changes;
        }

        private static T FirstOrDefault<T>(IEnumerator en, Func<T, bool> predicate)
        {
            while (en != null && en.MoveNext())
            {
                if (predicate((T)en.Current))
                {
                    return (T)en.Current;
                }
            }
            return default(T);
        }

        private static bool Any<T>(IEnumerator en, Func<T, bool> predicate)
        {
            while (en != null && en.MoveNext())
            {
                if (predicate((T)en.Current))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
