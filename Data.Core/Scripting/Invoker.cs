using Common;
using Data.Core.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Scripting
{
    public static class Invoker
    {
        static Dictionary<Type, InvocationMap> _mapped = new Dictionary<Type, InvocationMap>();

        public static IEnumerable<T> AsEnumerable<T>(object value)
        {
            if (value is IEnumerable<T>)
                return (IEnumerable<T>)value;
            else
                return new T[] { (T)value };
        }

        public static TResult Cast<TResult>(object value)
        {
            object cast;
            if (value != null)
            {
                if (value.GetType().Implements<IPath>() && typeof(TResult).Implements<IModel>())
                {
                    value = ((IPath)value).Root;
                }
                else if (value.GetType().Implements<IModel>() && typeof(TResult).Implements<IPath>())
                {
                    return (TResult)Path.Create(typeof(TResult).GetGenericArguments()[0], value as IModel, new IModel[0], new ILink[0]);
                }
            }

            if (ValueTypesEx.TryCast(value, typeof(TResult), out cast))
            {
                return (TResult)cast;
            }
            else
            {
                throw new InvalidCastException(string.Format("Cannot cast '{0}' to '{1}'.", value.GetType().Name, typeof(TResult).Name));
            }
        }

        public static IEnumerable<TResult> Cast<TResult>(this IEnumerable source)
        {
            var en = source.GetEnumerator();
            while (en.MoveNext())
            {
                object value = en.Current;
                yield return Cast<TResult>(value);
            }
        }

        public static object Sum<TSource>(this IQueryable<TSource> source, Expression<Func<TSource, object>> selector)
        {
            if (!source.Any()) return 0d;
            var value = selector.Compile()(source.FirstOrDefault());

            if (value == null) return 0d;

            var valueType = value.GetType();

            if (valueType != typeof(float)
                && valueType != typeof(long)
                && valueType != typeof(double)
                && valueType != typeof(decimal)
                && valueType != typeof(int)) return 0d;

            var linqMethod = typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                               .SingleOrDefault(mi =>
                                                        mi.Name.Equals("Sum")
                                                        && mi.GetParameters().Length == 2
                                                        && mi.GetParameters()[1].ParameterType.GetGenericArguments()[0].GetGenericArguments()[1].Equals(valueType))
                                               .MakeGenericMethod(typeof(TSource));
            var funcType = typeof(Func<,>).MakeGenericType(typeof(TSource), valueType);

            var funcParams = new ParameterExpression[]
            {
                Expression.Parameter(typeof(IQueryable<TSource>)),
                Expression.Parameter(typeof(Expression<>).MakeGenericType(funcType))
            };

            // need to convert the selector from Func<TSource, object> to Func<TSource, xxx>> where xxx is double, float, long, etc
            var newSelector = Expression.Lambda(funcType,
                                                Expression.Convert(
                                                        Expression.Invoke(selector, selector.Parameters[0]), // call current selector to get value as object
                                                        valueType), // cast value to proper type
                                                selector.Parameters[0]); // return proper type value

            var linqMethodCall = Expression.Call(null, linqMethod, funcParams[0], funcParams[1]); // call the appropriate Average method
            var func = Expression.Lambda(linqMethodCall, funcParams);

            return func.Compile().DynamicInvoke(source, newSelector); // invoke and return values
        }

        public static object Min<TSource>(this IQueryable<TSource> source, Expression<Func<TSource, object>> selector)
        {
            if (!source.Any()) return null;

            var value = selector.Compile()(source.FirstOrDefault(v => v != null));

            if (value == null) return null;

            var valueType = value.GetType();

            var linqMethod = typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                               .SingleOrDefault(mi =>
                                                        mi.Name.Equals("Min")
                                                        && mi.GetParameters().Length == 2
                                                        && mi.GetParameters()[1].ParameterType.GetGenericArguments()[0].GetGenericArguments()[1].Equals(valueType))
                                               .MakeGenericMethod(typeof(TSource));
            var funcType = typeof(Func<,>).MakeGenericType(typeof(TSource), valueType);

            var funcParams = new ParameterExpression[]
            {
                Expression.Parameter(typeof(IQueryable<TSource>)),
                Expression.Parameter(typeof(Expression<>).MakeGenericType(funcType))
            };

            // need to convert the selector from Func<TSource, object> to Func<TSource, xxx>> where xxx is double, float, long, etc
            var newSelector = Expression.Lambda(funcType,
                                                Expression.Convert(
                                                        Expression.Invoke(selector, selector.Parameters[0]), // call current selector to get value as object
                                                        valueType), // cast value to proper type
                                                selector.Parameters[0]); // return proper type value

            var linqMethodCall = Expression.Call(null, linqMethod, funcParams[0], funcParams[1]); // call the appropriate Average method
            var func = Expression.Lambda(linqMethodCall, funcParams);

            return func.Compile().DynamicInvoke(source, newSelector); // invoke and return values
        }

        public static object Max<TSource>(this IQueryable<TSource> source, Expression<Func<TSource, object>> selector)
        {
            if (!source.Any()) return null;

            var value = selector.Compile()(source.FirstOrDefault(v => v != null));

            if (value == null) return null;

            var valueType = value.GetType();

            var linqMethod = typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                               .SingleOrDefault(mi =>
                                                        mi.Name.Equals("Max")
                                                        && mi.GetParameters().Length == 2
                                                        && mi.GetParameters()[1].ParameterType.GetGenericArguments()[0].GetGenericArguments()[1].Equals(valueType))
                                               .MakeGenericMethod(typeof(TSource));
            var funcType = typeof(Func<,>).MakeGenericType(typeof(TSource), valueType);

            var funcParams = new ParameterExpression[]
            {
                Expression.Parameter(typeof(IQueryable<TSource>)),
                Expression.Parameter(typeof(Expression<>).MakeGenericType(funcType))
            };

            // need to convert the selector from Func<TSource, object> to Func<TSource, xxx>> where xxx is double, float, long, etc
            var newSelector = Expression.Lambda(funcType,
                                                Expression.Convert(
                                                        Expression.Invoke(selector, selector.Parameters[0]), // call current selector to get value as object
                                                        valueType), // cast value to proper type
                                                selector.Parameters[0]); // return proper type value

            var linqMethodCall = Expression.Call(null, linqMethod, funcParams[0], funcParams[1]); // call the appropriate Average method
            var func = Expression.Lambda(linqMethodCall, funcParams);

            return func.Compile().DynamicInvoke(source, newSelector); // invoke and return values
        }

        public static object Average<TSource>(this IQueryable<TSource> source, Expression<Func<TSource, object>> selector)
        {
            if (!source.Any()) return 0d;
            var value = selector.Compile()(source.FirstOrDefault());

            if (value == null) return 0d;

            var valueType = value.GetType();

            if (valueType != typeof(float)
                && valueType != typeof(long)
                && valueType != typeof(double)
                && valueType != typeof(decimal)
                && valueType != typeof(int)) return 0d;

            var linqMethod = typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                               .SingleOrDefault(mi =>
                                                        mi.Name.Equals("Average")
                                                        && mi.GetParameters().Length == 2
                                                        && mi.GetParameters()[1].ParameterType.GetGenericArguments()[0].GetGenericArguments()[1].Equals(valueType))
                                               .MakeGenericMethod(typeof(TSource));
            var funcType = typeof(Func<,>).MakeGenericType(typeof(TSource), valueType);

            var funcParams = new ParameterExpression[]
            {
                Expression.Parameter(typeof(IQueryable<TSource>)),
                Expression.Parameter(typeof(Expression<>).MakeGenericType(funcType))
            };

            // need to convert the selector from Func<TSource, object> to Func<TSource, xxx>> where xxx is double, float, long, etc
            var newSelector = Expression.Lambda(funcType,
                                                Expression.Convert(
                                                        Expression.Invoke(selector, selector.Parameters[0]), // call current selector to get value as object
                                                        valueType), // cast value to proper type
                                                selector.Parameters[0]); // return proper type value

            var linqMethodCall = Expression.Call(null, linqMethod, funcParams[0], funcParams[1]); // call the appropriate Average method
            var func = Expression.Lambda(linqMethodCall, funcParams);

            return func.Compile().DynamicInvoke(source, newSelector); // invoke and return values
        }

        public static bool GT(object left, object right)
        {
            if (left == null && right == null) return false;
            if (left == null) return false;
            if (right == null) return true;

            if (left.GetType().IsValueType && right.GetType().IsValueType)
            {
                var parms = new ParameterExpression[]
                {
                    Expression.Parameter(typeof(object)),
                    Expression.Parameter(typeof(object))
                };
                Type castType;
                Compare(left, right, out castType);
                var exp = Expression.GreaterThan(Expression.Convert(parms[0], castType), Expression.Convert(parms[1], castType));
                return Expression.Lambda<Func<object, object, bool>>(exp, parms).Compile()(left, right);
            }
            else return false;
        }

        public static bool GTE(object left, object right)
        {
            if (left == null && right == null) return true;
            if (left == null) return false;
            if (right == null) return true;

            if (left.GetType().IsValueType && right.GetType().IsValueType)
            {
                var parms = new ParameterExpression[]
                {
                    Expression.Parameter(typeof(object)),
                    Expression.Parameter(typeof(object))
                };
                Type castType;
                Compare(left, right, out castType);
                var exp = Expression.GreaterThanOrEqual(Expression.Convert(parms[0], castType), Expression.Convert(parms[1], castType));
                return Expression.Lambda<Func<object, object, bool>>(exp, parms).Compile()(left, right);
            }
            else return false;
        }

        public static bool LT(object left, object right)
        {
            if (left == null && right == null) return false;
            if (left == null) return true;
            if (right == null) return false;

            if (left.GetType().IsValueType && right.GetType().IsValueType)
            {
                var parms = new ParameterExpression[]
                {
                    Expression.Parameter(typeof(object)),
                    Expression.Parameter(typeof(object))
                };
                Type castType;
                Compare(left, right, out castType);
                var exp = Expression.LessThan(Expression.Convert(parms[0], castType), Expression.Convert(parms[1], castType));
                return Expression.Lambda<Func<object, object, bool>>(exp, parms).Compile()(left, right);
            }
            else return false;
        }

        public static bool LTE(object left, object right)
        {
            if (left == null && right == null) return true;
            if (left == null) return true;
            if (right == null) return false;

            if (left.GetType().IsValueType && right.GetType().IsValueType)
            {
                var parms = new ParameterExpression[]
                {
                    Expression.Parameter(typeof(object)),
                    Expression.Parameter(typeof(object))
                };
                Type castType;
                Compare(left, right, out castType);
                var exp = Expression.LessThanOrEqual(Expression.Convert(parms[0], castType), Expression.Convert(parms[1], castType));
                return Expression.Lambda<Func<object, object, bool>>(exp, parms).Compile()(left, right);
            }
            else return false;
        }

        private static void Compare(object left, object right, out Type castType)
        {
            var leftSize = Marshal.SizeOf(left);
            var rightSize = Marshal.SizeOf(right);
            var leftType = left.GetType();
            var rightType = right.GetType();
            if (leftSize > rightSize)
            {
                castType = leftType;
            }
            else if (leftSize < rightSize)
            {
                castType = rightType;
            }
            else if (leftType.Equals(rightType))
            {
                castType = leftType;
            }
            else
            {
                // sizes are equal, need to see which is floating point
                if (rightType == typeof(float) || rightType == typeof(double))
                {
                    castType = rightType;
                }
                else
                {
                    castType = leftType;
                }
            }
        }

        public static object InvokeMethod(object source, string name, params object[] args)
        {
            if (source == null) return null;
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
                    var methods = sourceType.GetMethods().Where(mi => mi.Name.Equals(name) && mi.GetParameters().Length == args.Length);
                    MethodInfo method = null;

                    foreach (var m in methods)
                    {
                        method = m;
                        if (m.IsGenericMethodDefinition)
                        {
                            var genArgs = m.GetGenericArguments();
                            if (genArgs.Length > 1) continue;

                            var genArg = genArgs[0];
                            if (genArg.Implements<IModel>() && args[0] is IModel)
                            {
                                method = method.MakeGenericMethod(((IModel)args[0]).ModelType);
                            }
                        }

                        var parms = m.GetParameters();
                        for (int p = 0; p < args.Length; p++)
                        {
                            if (!args[p].GetType().IsConvertibleTo(parms[p].ParameterType))
                            {
                                method = null;
                                break;
                            }
                        }
                        if (method != null)
                        {
                            break;
                        }
                    }
                    

                    Type lambdaType = null;
                    Type returnType = method == null ? typeof(object) : method.ReturnType;
                    Type[] genTypes = new Type[] { sourceType }.Concat(args.Select(a => a.GetType())).Concat(returnType.Equals(typeof(void)) ? Type.EmptyTypes : new Type[] { returnType }).ToArray();
                    var isVoid = returnType.Equals(typeof(void));

                    switch (args.Length)
                    {
                        default:
                            {
                                throw new NotSupportedException(string.Format("A method signature with {0} parameters is not supported", args.Length));
                            }
                        case 0:
                            {
                                if (isVoid)
                                    lambdaType = typeof(Action<>);
                                else
                                    lambdaType = typeof(Func<,>);
                                break;
                            }
                        case 1:
                            {
                                if (isVoid)
                                    lambdaType = typeof(Action<,>);
                                else
                                    lambdaType = typeof(Func<,,>);
                                break;
                            }
                        case 2:
                            {
                                if (isVoid)
                                    lambdaType = typeof(Action<,,>);
                                else
                                    lambdaType = typeof(Func<,,,>);
                                break;
                            }
                        case 3:
                            {
                                if (isVoid)
                                    lambdaType = typeof(Action<,,,>);
                                else
                                    lambdaType = typeof(Func<,,,,>);
                                break;
                            }
                        case 4:
                            {
                                if (isVoid)
                                    lambdaType = typeof(Action<,,,,>);
                                else
                                    lambdaType = typeof(Func<,,,,,>);
                                break;
                            }
                        case 5:
                            {
                                if (isVoid)
                                    lambdaType = typeof(Action<,,,,,>);
                                else
                                    lambdaType = typeof(Func<,,,,,,>);
                                break;
                            }
                        case 6:
                            {
                                if (isVoid)
                                    lambdaType = typeof(Action<,,,,,,>);
                                else
                                    lambdaType = typeof(Func<,,,,,,,>);
                                break;
                            }
                        case 7:
                            {
                                if (isVoid)
                                    lambdaType = typeof(Action<,,,,,,,>);
                                else
                                    lambdaType = typeof(Func<,,,,,,,,>);
                                break;
                            }
                        case 8:
                            {
                                if (isVoid)
                                    lambdaType = typeof(Action<,,,,,,,,>);
                                else
                                    lambdaType = typeof(Func<,,,,,,,,,>);
                                break;
                            }
                        case 9:
                            {
                                if (isVoid)
                                    lambdaType = typeof(Action<,,,,,,,,,>);
                                else
                                    lambdaType = typeof(Func<,,,,,,,,,,>);
                                break;
                            }
                        case 10:
                            {
                                if (isVoid)
                                    lambdaType = typeof(Action<,,,,,,,,,,>);
                                else
                                    lambdaType = typeof(Func<,,,,,,,,,,,>);
                                break;
                            }
                    }

                    lambdaType = lambdaType.MakeGenericType(genTypes);

                    if (method == null)
                    {
                        Expression defaultValue;
                        if (method.ReturnType.IsValueType)
                        {
                            var ctor = method.ReturnType.GetConstructor(Type.EmptyTypes);
                            defaultValue = Expression.New(ctor);
                        }
                        else
                        {
                            defaultValue = Expression.Constant(null);
                        }
                        del = Expression.Lambda(lambdaType, Expression.Block(defaultValue)).Compile();
                    }
                    else
                    {
                        var sourceParam = Expression.Parameter(sourceType, "source");
                        var methodParms = args == null || args.Length == 0 ? new ParameterExpression[0] : args.Select(a => Expression.Parameter(a.GetType())).ToArray();
                        var parms = new ParameterExpression[] { sourceParam }.Concat(methodParms).ToArray();

                        var exp = Expression.Lambda(lambdaType, Expression.Call(sourceParam, method, methodParms), parms);
                        del = exp.Compile();
                    }
                    map.Add(name, del);
                }
            }

            if (args.Length == 0)
            {
                return del.DynamicInvoke(source);
            }
            else
            {
                return del.DynamicInvoke(new object[] { source }.Concat(args).ToArray());
            }
        }
    }
}
