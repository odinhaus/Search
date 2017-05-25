using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public unsafe static class ValueTypesEx
    {
        public static bool IsNullable(this Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
        }

        public static string ToISO8601(this DateTime datetime)
        {
            return datetime.ToUniversalTime().ToString("o");
        }

        public static bool TryCast<T>(this object value, out T cast)
        {
            object castObj;
            cast = default(T);
            var result = TryCast(value, typeof(T), out castObj);
            if (result)
            {
                cast = (T)castObj;
            }
            return result;
        }

        public static bool TryCast(this object value, Type type, out object cast)
        {
            cast = null;
            if (value == null)
            {
                if (type.IsValueType)
                {
                    cast = Activator.CreateInstance(type, Type.EmptyTypes);
                }
                return true;
            }

            try
            {
                if (value is string && type == typeof(DateTime))
                {
                    cast = (object)DateTime.Parse(value.ToString());
                    return true;
                }
                else if (value is string && type.IsEnum)
                {
                    cast = Enum.Parse(type, value.ToString());
                    return true;
                }
                else if (value is string && type == typeof(byte[]))
                {
                    cast = (object)Convert.FromBase64String(value.ToString());
                    return true;
                }
                else if (value is IEnumerable && type.IsArray)
                {
                    var en = ((IEnumerable)value).GetEnumerator();
                    var arrayType = type.GetElementType();
                    var list = Activator.CreateInstance(typeof(List<>).MakeGenericType(arrayType)) as IList;

                    var p = Expression.Parameter(typeof(object));
                    var conv = Expression.Convert(Expression.Convert(p, typeof(object)), arrayType);
                    var func = typeof(Func<,>).MakeGenericType(typeof(object), arrayType);
                    var lambda = Expression.Lambda(func, conv, p).Compile();

                    var toArrayMethod = list.GetType().GetMethod("ToArray", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    var arrayParam = Expression.Parameter(typeof(IList));
                    var toArrayCall = Expression.Call(Expression.Convert(arrayParam, list.GetType()), toArrayMethod);
                    var lambdaType = typeof(Func<,>).MakeGenericType(typeof(IList), type);
                    var toArrayFunc = Expression.Lambda(lambdaType, toArrayCall, arrayParam).Compile();

                    while (en.MoveNext())
                    {
                        list.Add(lambda.DynamicInvoke(en.Current));
                    }

                    cast = toArrayFunc.DynamicInvoke(list);
                    return true;
                }
                else if (type == typeof(string))
                {
                    cast = value.ToString();
                    return true;
                }
                else if (type.Implements(typeof(Nullable<>)))
                {
                    if (value == null)
                    {
                        cast = Activator.CreateInstance(type);
                        return true;
                    }
                    else
                    {
                        var baseType = type.GetGenericArguments()[0];
                        object firstCast;
                        if (TryCast(value, baseType, out firstCast))
                        {
                            cast = Activator.CreateInstance(type, firstCast);
                            return true;
                        }
                        else
                        {
                            throw new InvalidCastException(string.Format("Cannot cast object of type {0} to {1}.", value.GetType().Name, type.Name));
                        }
                    }
                }
                else if (value.GetType().Equals(typeof(Dictionary<string, object>)))
                {
                    return false;
                }
                else
                {
                    var p = Expression.Parameter(value.GetType());
                    var conv = Expression.Convert(p, type);
                    var func = typeof(Func<,>).MakeGenericType(value.GetType(), type);
                    var lambda = Expression.Lambda(func, conv, p).Compile();
                    cast = lambda.DynamicInvoke(value);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public static byte[] GetBytes(this IComparable value)
        {
            if (value is bool)
                return GetBytes((bool)value);
            else if (value is byte)
                return GetBytes((byte)value);
            else if (value is char)
                return GetBytes((char)value);
            else if (value is ushort)
                return GetBytes((ushort)value);
            else if (value is short)
                return GetBytes((short)value);
            else if (value is uint)
                return GetBytes((uint)value);
            else if (value is int)
                return GetBytes((int)value);
            else if (value is float)
                return GetBytes((float)value);
            else if (value is ulong)
                return GetBytes((ulong)value);
            else if (value is long)
                return GetBytes((long)value);
            else if (value is double)
                return GetBytes((double)value);
            else if (value is decimal)
                return GetBytes((decimal)value);
            else if (value is string)
                return GetBytes((string)value);
            else if (value is DateTime)
                return GetBytes((DateTime)value);
            else
                throw (new InvalidOperationException("Type not supported"));
        }

        public static byte[] GetBytes(this bool value)
        {
            byte[] bytes = new byte[1];
            fixed (byte* ptr = bytes)
            {
                *(bool*)ptr = value;
                return bytes;
            }
        }

        public static byte[] GetBytes(this byte value)
        {
            byte[] bytes = new byte[1];
            fixed (byte* ptr = bytes)
            {
                *(byte*)ptr = value;
                return bytes;
            }
        }

        public static byte[] GetBytes(this char value)
        {
            byte[] bytes = new byte[2];
            fixed (byte* ptr = bytes)
            {
                *(char*)ptr = value;
                return bytes;
            }
        }

        public static byte[] GetBytes(this short value)
        {
            byte[] bytes = new byte[2];
            fixed (byte* ptr = bytes)
            {
                *(short*)ptr = value;
                return bytes;
            }
        }

        public static byte[] GetBytes(this ushort value)
        {
            byte[] bytes = new byte[2];
            fixed (byte* ptr = bytes)
            {
                *(ushort*)ptr = value;
                return bytes;
            }
        }

        public static byte[] GetBytes(this int value)
        {
            byte[] bytes = new byte[4];
            fixed (byte* ptr = bytes)
            {
                *(int*)ptr = value;
                return bytes;
            }
        }

        public static byte[] GetBytes(this uint value)
        {
            byte[] bytes = new byte[4];
            fixed (byte* ptr = bytes)
            {
                *(uint*)ptr = value;
                return bytes;
            }
        }

        public static byte[] GetBytes(this float value)
        {
            byte[] bytes = new byte[4];
            fixed (byte* ptr = bytes)
            {
                *(float*)ptr = value;
                return bytes;
            }
        }

        public static byte[] GetBytes(this long value)
        {
            byte[] bytes = new byte[8];
            fixed (byte* ptr = bytes)
            {
                *(long*)ptr = value;
                return bytes;
            }
        }

        public static byte[] GetBytes(this ulong value)
        {
            byte[] bytes = new byte[8];
            fixed (byte* ptr = bytes)
            {
                *(ulong*)ptr = value;
                return bytes;
            }
        }

        public static byte[] GetBytes(this double value)
        {
            byte[] bytes = new byte[8];
            fixed (byte* ptr = bytes)
            {
                *(double*)ptr = value;
                return bytes;
            }
        }

        public static byte[] GetBytes(this decimal value)
        {
            byte[] bytes = new byte[8];
            fixed (byte* ptr = bytes)
            {
                *(decimal*)ptr = value;
                return bytes;
            }
        }

        public static byte[] GetBytes(this DateTime value)
        {
            return value.ToBinary().GetBytes();
        }

        public static byte[] GetBytes(this string value)
        {
            int count = Encoding.UTF8.GetByteCount(value);
            byte[] text = new byte[4 + count];
            count.GetBytes(ref text);
            Encoding.UTF8.GetBytes(value).CopyTo(text, 4);
            return text;
        }

        //========================


        public static void GetBytes(this bool value, ref byte[] bytes)
        {
            fixed (byte* ptr = bytes)
            {
                *(bool*)ptr = value;
            }
        }

        public static void GetBytes(this byte value, ref byte[] bytes)
        {
            fixed (byte* ptr = bytes)
            {
                *(byte*)ptr = value;
            }
        }

        public static void GetBytes(this char value, ref byte[] bytes)
        {
            fixed (byte* ptr = bytes)
            {
                *(char*)ptr = value;
            }
        }

        public static void GetBytes(this short value, ref byte[] bytes)
        {
            fixed (byte* ptr = bytes)
            {
                *(short*)ptr = value;
            }
        }



        public static void GetBytes(this ushort value, ref byte[] bytes)
        {
            fixed (byte* ptr = bytes)
            {
                *(ushort*)ptr = value;
            }
        }

        public static void GetBytes(this int value, ref byte[] bytes)
        {
            fixed (byte* ptr = bytes)
            {
                *(int*)ptr = value;
            }
        }

        public static void GetBytes(this uint value, ref byte[] bytes)
        {
            fixed (byte* ptr = bytes)
            {
                *(uint*)ptr = value;
            }
        }

        public static void GetBytes(this float value, ref byte[] bytes)
        {
            fixed (byte* ptr = bytes)
            {
                *(float*)ptr = value;
            }
        }

        public static void GetBytes(this long value, ref byte[] bytes)
        {
            fixed (byte* ptr = bytes)
            {
                *(long*)ptr = value;
            }
        }

        public static void GetBytes(this ulong value, ref byte[] bytes)
        {
            fixed (byte* ptr = bytes)
            {
                *(ulong*)ptr = value;
            }
        }

        public static void GetBytes(this double value, ref byte[] bytes)
        {
            fixed (byte* ptr = bytes)
            {
                *(double*)ptr = value;
            }
        }

        public static void GetBytes(this decimal value, ref byte[] bytes)
        {
            fixed (byte* ptr = bytes)
            {
                *(decimal*)ptr = value;
            }
        }

        public static void GetBytes(this DateTime value, ref byte[] bytes)
        {
            value.ToBinary().GetBytes(ref bytes);
        }


        //=======================

        public static char ToChar(this byte[] bytes)
        {
            fixed (byte* ptr = bytes)
            {
                return *(((char*)ptr));
            }
        }

        public static ushort ToUInt16(this byte[] bytes)
        {
            fixed (byte* ptr = bytes)
            {
                return *(((ushort*)ptr));
            }
        }

        public static short ToInt16(this byte[] bytes)
        {
            fixed (byte* ptr = bytes)
            {
                return *(((short*)ptr));
            }
        }

        public static float ToSingle(this byte[] bytes)
        {
            fixed (byte* ptr = bytes)
            {
                return *(((float*)ptr));
            }
        }

        public static double ToDouble(this byte[] bytes)
        {
            fixed (byte* ptr = bytes)
            {
                return *(((double*)ptr));
            }
        }

        public static uint ToUInt32(this byte[] bytes)
        {
            fixed (byte* ptr = bytes)
            {
                return *(((uint*)ptr));
            }
        }

        public static uint ToUInt32(this byte[] bytes, int index)
        {
            fixed (byte* ptr = bytes)
            {
                byte* ptr2 = ptr;
                ptr2 += index;
                return *(((uint*)ptr2));
            }
        }

        public static int ToInt32(this byte[] bytes)
        {
            fixed (byte* ptr = bytes)
            {
                return *(((int*)ptr));
            }
        }

        public static ulong ToUInt64(this byte[] bytes)
        {
            fixed (byte* ptr = bytes)
            {
                return *(((ulong*)ptr));
            }
        }

        public static long ToInt64(this byte[] bytes)
        {
            fixed (byte* ptr = bytes)
            {
                return *(((long*)ptr));
            }
        }

        public static decimal ToDecimal(this byte[] bytes)
        {
            fixed (byte* ptr = bytes)
            {
                return *(((decimal*)ptr));
            }
        }

        public static bool ToBoolean(this byte[] bytes)
        {
            fixed (byte* ptr = bytes)
            {
                return *(((bool*)ptr));
            }
        }

        public static DateTime ToDateTime(this byte[] bytes)
        {
            fixed (byte* ptr = bytes)
            {
                return DateTime.FromBinary(*(((long*)ptr)));
            }
        }

        public static bool IsNumeric(this object value)
        {
            if (value == null) return false;
            if (value is byte
                || value is ushort
                || value is short
                || value is uint
                || value is int
                || value is ulong
                || value is long
                || value is float
                || value is double
                || value is decimal
                || value is byte?
                || value is ushort?
                || value is short?
                || value is uint?
                || value is int?
                || value is ulong?
                || value is long?
                || value is float?
                || value is double?
                || value is decimal?)
                return true;
            return false;
        }

        public static bool IsNumeric(this Type value)
        {
            if (value == null) return false;
            if (value == typeof(byte)
                || value == typeof(ushort)
                || value == typeof(short)
                || value == typeof(uint)
                || value == typeof(int)
                || value == typeof(ulong)
                || value == typeof(long)
                || value == typeof(float)
                || value == typeof(double)
                || value == typeof(decimal)
                || value == typeof(byte?)
                || value == typeof(ushort?)
                || value == typeof(short?)
                || value == typeof(uint?)
                || value == typeof(int?)
                || value == typeof(ulong?)
                || value == typeof(long?)
                || value == typeof(float?)
                || value == typeof(double?)
                || value == typeof(decimal?))
                return true;
            return false;
        }

        public static bool IsDateTime(this Type value)
        {
            return value.Equals(typeof(DateTime));
        }

        public static bool IsZero(this object value)
        {
            if (value is byte)
                return (byte)value == (byte)0;
            else if (value is ushort)
                return (ushort)value == (ushort)0;
            else if (value is short)
                return (short)value == (short)0;
            else if (value is uint)
                return (uint)value == (uint)0;
            else if (value is int)
                return (int)value == (int)0;
            else if (value is ulong)
                return (ulong)value == (ulong)0;
            else if (value is long)
                return (long)value == (long)0;
            else if (value is float)
                return (float)value == (float)0;
            else if (value is double)
                return (double)value == (double)0;
            else if (value is decimal)
                return (decimal)value == (decimal)0;
            else if (value is DateTime)
                return ((DateTime)value).Ticks == 0;
            else
                return value == null;
        }
    }
}
