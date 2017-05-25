using Common;
using Data.Core.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Scripting
{
    public class PropertyInvoker
    {
        static Dictionary<Type, InvocationMap> _mapped = new Dictionary<Type, InvocationMap>();

        public PropertyInvoker(object source, string propertyName)
        {
            this.Source = source;
            this.PropertyName = propertyName;
            this.Getter = MakeGetter(source, propertyName);
            this.Setter = MakeSetter(source, propertyName);
        }

        public string PropertyName { get; private set; }
        public object Source { get; private set; }
        public Delegate Getter { get; private set; }
        public Delegate Setter { get; private set; }

        public object Property
        {
            get
            {
                if (Getter == null) return null;
                return Getter.DynamicInvoke(this.Source);
            }
            set
            {
                if (Setter != null)
                {
                    Setter.DynamicInvoke(this.Source, value);
                }
            }
        }

        private Delegate MakeGetter(object source, string name)
        {
            if (source == null) return null;
            name = "get_" + name;
            var sourceType = source.GetType();
            InvocationMap map;
            Delegate del;
            lock (_mapped)
            {

                if (!_mapped.TryGetValue(sourceType, out map))
                {
                    map = new InvocationMap();
                    _mapped.Add(sourceType, map);
                }

                if (!map.TryGet(name, out del))
                {
                    var prop = sourceType.GetPublicProperties().FirstOrDefault(pi => name.Equals("get_" + pi.Name));
                    if (prop == null && sourceType.IsGenericType && sourceType.GetGenericTypeDefinition().Equals(typeof(Path<>)))
                    {
                        this.Source = ((IPath)source).Root;
                        return MakeGetter(((IPath)source).Root, name.Replace("get_", ""));
                    }
                    var lambdaType = typeof(Func<,>).MakeGenericType(sourceType, prop?.PropertyType ?? typeof(object));
                    var sourceParam = Expression.Parameter(sourceType, "source");
                    if (prop == null || !prop.CanRead)
                    {
                        if (PropertyName.Equals("Count") && sourceType.Implements<IEnumerable>())
                        {
                            var count = typeof(Enumerable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                                          .Single(mi => mi.Name.Equals(PropertyName) && mi.GetParameters().Length == 1)
                                                          .MakeGenericMethod(source.GetType().GetGenericArguments()[0]);
                            var countCall = Expression.Convert(Expression.Call(null, count, sourceParam), typeof(object));
                            del = Expression.Lambda(lambdaType, countCall, new ParameterExpression[] { sourceParam }).Compile();
                        }
                        else
                        {
                            Expression defaultValue;
                            if (prop?.PropertyType.IsValueType ?? false)
                            {
                                var ctor = prop.PropertyType.GetConstructor(Type.EmptyTypes);
                                defaultValue = Expression.New(ctor);
                            }
                            else
                            {
                                defaultValue = Expression.Constant(null);
                            }

                            del = Expression.Lambda(lambdaType, Expression.Block(defaultValue), new ParameterExpression[] { sourceParam }).Compile();
                        }
                    }
                    else
                    {
                        var getter = prop.GetMethod;

                        var exp = Expression.Lambda(lambdaType, Expression.Call(sourceParam, getter), new ParameterExpression[] { sourceParam });
                        del = exp.Compile();
                    }
                    map.Add(name, del);
                }
            }

            return del;
        }

        private Delegate MakeSetter(object source, string name)
        {
            if (source == null) return null;

            name = "set_" + name;
            var sourceType = source.GetType();
            InvocationMap map;
            Delegate del;
            lock (_mapped)
            {

                if (!_mapped.TryGetValue(sourceType, out map))
                {
                    map = new InvocationMap();
                    _mapped.Add(sourceType, map);
                }

                if (!map.TryGet(name, out del))
                {
                    var prop = sourceType.GetPublicProperties().FirstOrDefault(pi => name.Equals("set_" + pi.Name));
                    if (prop == null && sourceType.IsGenericType && sourceType.GetGenericTypeDefinition().Equals(typeof(Path<>)))
                    {
                        this.Source = ((IPath)source).Root;
                        MakeSetter(((IPath)source).Root, name.Replace("set_", ""));
                    }
                    if (prop == null || !prop.CanWrite)
                    {
                        del = null;
                    }
                    else
                    {
                        var sourceParam = Expression.Parameter(sourceType, "source");
                        var valueParam = Expression.Parameter(prop.PropertyType, "value");
                        var setter = prop.SetMethod;
                        var lambdaType = typeof(Action<,>).MakeGenericType(sourceType, prop.PropertyType);
                        del = Expression.Lambda(lambdaType, Expression.Call(sourceParam, setter, valueParam), sourceParam, valueParam).Compile();
                    }
                    map.Add(name, del);
                }
            }

            return del;
        }
    }

}
