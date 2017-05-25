using Common;
using Common.Serialization.Binary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Linq
{
    public abstract class BinarySerializableExpression : Expression, IBinarySerializable
    {
        protected BinarySerializableExpression()
        {
            ProtocolBuffer = new byte[0];
        }

        public byte[] ProtocolBuffer
        {
            get;
            set;
        }


        public byte[] ToBytes()
        {
            using (var ms = new MemoryStream())
            {
                using (var bw = new BinaryWriter(ms))
                {
                    OnToBytes(bw);
                    bw.Write(ProtocolBuffer.Length);
                    bw.Write(ProtocolBuffer);
                    return ms.ToArray();
                }
            }
        }

        protected abstract void OnToBytes(BinaryWriter bw);

        public void FromBytes(byte[] source)
        {
            using (var ms = new MemoryStream(source))
            {
                using (var br = new BinaryReader(ms))
                {
                    OnFromBytes(br);
                    ProtocolBuffer = br.ReadBytes(br.ReadInt32());
                }
            }
        }

        protected abstract void OnFromBytes(BinaryReader br);

        protected virtual BinarySerializableExpression CreateExpression(QueryExpressionType predType)
        {
            switch (predType)
            {
                case QueryExpressionType.And:
                    return new AndExpression();
                case QueryExpressionType.Contains:
                    return new ContainsExpression();
                case QueryExpressionType.EQ:
                    return new EQExpression();
                case QueryExpressionType.GT:
                    return new GTExpression();
                case QueryExpressionType.GTE:
                    return new GTEExpression();
                case QueryExpressionType.InEdgeNodeFilter:
                    return new InEdgeNodeFilterExpression();
                case QueryExpressionType.LT:
                    return new LTExpression();
                case QueryExpressionType.LTE:
                    return new LTEExpression();
                case QueryExpressionType.NE:
                    return new NEQExpression();
                case QueryExpressionType.Or:
                    return new OrExpression();
                case QueryExpressionType.OutEdgeNodeFilter:
                    return new OutEdgeNodeFilterExpression();
                case QueryExpressionType.PathEdgeFilterMember:
                    return new PathEdgeFilterMemberAccessExpression();
                case QueryExpressionType.PathNodeFilterMember:
                    return new PathNodeFilterMemberAccessExpression();
                case QueryExpressionType.PathRootFilter:
                    return new PathRootFilterExpression();
                case QueryExpressionType.Predicate:
                    return new PredicateExpression();
                case QueryExpressionType.StartsWith:
                    return new StartsWithExpression();
                case QueryExpressionType.Traverse:
                    return new TraverseExpression();
                case QueryExpressionType.TraverseOrigin:
                    return new TraverseOriginExpression();
                case QueryExpressionType.TraverseReturns:
                    return new TraverseReturnsExpression();
                case QueryExpressionType.Scalar:
                    return new ScalarExpression();
                case QueryExpressionType.Date_Add:
                case QueryExpressionType.Date_Day:
                case QueryExpressionType.Date_DayOfWeek:
                case QueryExpressionType.Date_DayOfYear:
                case QueryExpressionType.Date_Diff:
                case QueryExpressionType.Date_Hour:
                case QueryExpressionType.Date_ISO8601:
                case QueryExpressionType.Date_Millisecond:
                case QueryExpressionType.Date_Minute:
                case QueryExpressionType.Date_Month:
                case QueryExpressionType.Date_Second:
                case QueryExpressionType.Date_Subtract:
                case QueryExpressionType.Date_Timestamp:
                case QueryExpressionType.Date_Year:
                    return new DateFunctionExpression();
            }
            throw new InvalidOperationException("Expression type not supported");
        }

        protected void PrimitiveToBytes(object value, Type type, BinaryWriter bw)
        {
            if (type.Equals(typeof(string)))
            {
                bw.Write((int)PrimitiveType.String);
                bw.Write(value.ToString());
                return;
            }
            else if (type.Equals(typeof(DateTime)))
            {
                bw.Write((int)PrimitiveType.DateTime);
                bw.Write(((DateTime)value).ToBinary());
                return;
            }
            else if (type.Equals(typeof(bool)))
            {
                bw.Write((int)PrimitiveType.Boolean);
                bw.Write((bool)value);
                return;
            }
            else if (type.Equals(typeof(byte)))
            {
                bw.Write((int)PrimitiveType.Byte);
                bw.Write((byte)value);
                return;
            }
            else if (type.Equals(typeof(sbyte)))
            {
                bw.Write((int)PrimitiveType.SByte);
                bw.Write((sbyte)value);
                return;
            }
            else if (type.Equals(typeof(char)))
            {
                bw.Write((int)PrimitiveType.Char);
                bw.Write((char)value);
                return;
            }
            else if (type.Equals(typeof(short)))
            {
                bw.Write((int)PrimitiveType.Short);
                bw.Write((short)value);
                return;
            }
            else if (type.Equals(typeof(int)))
            {
                bw.Write((int)PrimitiveType.Int);
                bw.Write((int)value);
                return;
            }
            else if (type.Equals(typeof(long)))
            {
                bw.Write((int)PrimitiveType.Long);
                bw.Write((long)value);
                return;
            }
            else if (type.Equals(typeof(ushort)))
            {
                bw.Write((int)PrimitiveType.UShort);
                bw.Write((ushort)value);
                return;
            }
            else if (type.Equals(typeof(uint)))
            {
                bw.Write((int)PrimitiveType.UInt);
                bw.Write((uint)value);
                return;
            }
            else if (type.Equals(typeof(ulong)))
            {
                bw.Write((int)PrimitiveType.ULong);
                bw.Write((ulong)value);
                return;
            }
            else if (type.Equals(typeof(float)))
            {
                bw.Write((int)PrimitiveType.Float);
                bw.Write((float)value);
                return;
            }
            else if (type.Equals(typeof(double)))
            {
                bw.Write((int)PrimitiveType.Double);
                bw.Write((double)value);
                return;
            }
            else if (type.Equals(typeof(decimal)))
            {
                bw.Write((int)PrimitiveType.Decimal);
                bw.Write((decimal)value);
                return;
            }
            else if (type.Equals(typeof(string)))
            {
                bw.Write((int)PrimitiveType.String);
                var isNull = value == null;
                bw.Write(!isNull);
                if (!isNull)
                    bw.Write(value.ToString());
                return;
            }
            else if (type.Equals(typeof(DateTime)))
            {
                bw.Write((int)PrimitiveType.DateTime);
                bw.Write(((DateTime)value).ToBinary());
                return;
            }
            else if (type.Equals(typeof(bool?)))
            {
                bw.Write((int)PrimitiveType.NullableBoolean);
                var hasValue = ((bool?)value).HasValue;
                bw.Write(hasValue);
                if (hasValue)
                    bw.Write(((bool?)value).Value);
                return;
            }
            else if (type.Equals(typeof(byte?)))
            {
                bw.Write((int)PrimitiveType.NullableByte);
                var hasValue = ((byte?)value).HasValue;
                bw.Write(hasValue);
                if (hasValue)
                    bw.Write(((byte?)value).Value);
                return;
            }
            else if (type.Equals(typeof(sbyte?)))
            {
                bw.Write((int)PrimitiveType.NullableSByte);
                var hasValue = ((sbyte?)value).HasValue;
                bw.Write(hasValue);
                if (hasValue)
                    bw.Write(((sbyte?)value).Value);
                return;
            }
            else if (type.Equals(typeof(char?)))
            {
                bw.Write((int)PrimitiveType.NullableChar);
                var hasValue = ((char?)value).HasValue;
                bw.Write(hasValue);
                if (hasValue)
                    bw.Write(((char?)value).Value);
                return;
            }
            else if (type.Equals(typeof(short?)))
            {
                bw.Write((int)PrimitiveType.NullableShort);
                var hasValue = ((short?)value).HasValue;
                bw.Write(hasValue);
                if (hasValue)
                    bw.Write(((short?)value).Value);
                return;
            }
            else if (type.Equals(typeof(int?)))
            {
                bw.Write((int)PrimitiveType.NullableInt);
                var hasValue = ((int?)value).HasValue;
                bw.Write(hasValue);
                if (hasValue)
                    bw.Write(((int?)value).Value);
                return;
            }
            else if (type.Equals(typeof(long?)))
            {
                bw.Write((int)PrimitiveType.NullableLong);
                var hasValue = ((long?)value).HasValue;
                bw.Write(hasValue);
                if (hasValue)
                    bw.Write(((long?)value).Value);
                return;
            }
            else if (type.Equals(typeof(ushort?)))
            {
                bw.Write((int)PrimitiveType.NullableUShort);
                var hasValue = ((ushort?)value).HasValue;
                bw.Write(hasValue);
                if (hasValue)
                    bw.Write(((ushort?)value).Value);
                return;
            }
            else if (type.Equals(typeof(uint?)))
            {
                bw.Write((int)PrimitiveType.NullableUInt);
                var hasValue = ((uint?)value).HasValue;
                bw.Write(hasValue);
                if (hasValue)
                    bw.Write(((uint?)value).Value);
                return;
            }
            else if (type.Equals(typeof(ulong?)))
            {
                bw.Write((int)PrimitiveType.NullableULong);
                var hasValue = ((ulong?)value).HasValue;
                bw.Write(hasValue);
                if (hasValue)
                    bw.Write(((ulong?)value).Value);
                return;
            }
            else if (type.Equals(typeof(float?)))
            {
                bw.Write((int)PrimitiveType.NullableFloat);
                var hasValue = ((float?)value).HasValue;
                bw.Write(hasValue);
                if (hasValue)
                    bw.Write(((float?)value).Value);
                return;
            }
            else if (type.Equals(typeof(double?)))
            {
                bw.Write((int)PrimitiveType.NullableDouble);
                bw.Write((double)value);
                return;
            }
            else if (type.Equals(typeof(decimal?)))
            {
                bw.Write((int)PrimitiveType.NullableDecimal);
                var hasValue = ((decimal?)value).HasValue;
                bw.Write(hasValue);
                if (hasValue)
                    bw.Write(((decimal?)value).Value);
                return;
            }
            else if (type.Equals(typeof(DateTime?)))
            {
                bw.Write((int)PrimitiveType.NullableDateTime);
                var hasValue = ((DateTime?)value).HasValue;
                bw.Write(hasValue);
                if (hasValue)
                    bw.Write(((DateTime?)value).Value.ToBinary());
                return;
            }
            else if (type.IsEnum)
            {
                bw.Write((int)PrimitiveType.Enum);
                bw.Write(Type.AssemblyQualifiedName);
                object cast;
                if (value.TryCast(Enum.GetUnderlyingType(value.GetType()), out cast))
                {
                    PrimitiveToBytes(cast, cast.GetType(), bw);
                    return;
                }
            }
            else if (Type.IsGenericType && Type.GetGenericArguments()[0].IsEnum && Type.GetGenericTypeDefinition().Equals(typeof(Nullable<>)))
            {
                bw.Write((int)PrimitiveType.NullableEnum);
                bw.Write(Type.AssemblyQualifiedName);
                var hasValue = value == null ? false : (bool)Type.GetProperty("HasValue").GetValue(value);
                bw.Write(hasValue);
                if (hasValue)
                {
                    object cast;
                    object enumValue = Type.GetProperty("Value").GetValue(value);
                    if (enumValue.TryCast(Enum.GetUnderlyingType(Type.GetGenericArguments()[0]), out cast))
                    {
                        PrimitiveToBytes(cast, cast.GetType(), bw);
                        return;
                    }
                }
            }
     
            throw new NotSupportedException("The value provided is not a primitive type");
        }

        protected object PrimitiveFromBytes(BinaryReader br)
        {
            var type = (PrimitiveType)br.ReadInt32();
            switch(type)
            {
                case PrimitiveType.String:
                    return br.ReadString();
                case PrimitiveType.DateTime:
                    return DateTime.FromBinary(br.ReadInt64());
                case PrimitiveType.Boolean:
                    return br.ReadBoolean();
                case PrimitiveType.Byte:
                    return br.ReadByte();
                case PrimitiveType.Char:
                    return br.ReadChar();
                case PrimitiveType.Decimal:
                    return br.ReadDecimal();
                case PrimitiveType.Double:
                    return br.ReadDouble();
                case PrimitiveType.Float:
                    return br.ReadSingle();
                case PrimitiveType.Int:
                    return br.ReadInt32();
                case PrimitiveType.Long:
                    return br.ReadInt64();
                case PrimitiveType.SByte:
                    return br.ReadSByte();
                case PrimitiveType.Short:
                    return br.ReadInt16();
                case PrimitiveType.UInt:
                    return br.ReadUInt32();
                case PrimitiveType.ULong:
                    return br.ReadUInt64();
                case PrimitiveType.UShort:
                    return br.ReadUInt16();
                case PrimitiveType.NullableDateTime:
                    {
                        if (br.ReadBoolean())
                        {
                            return DateTime.FromBinary(br.ReadInt64());
                        }
                        else return null;
                    }
                case PrimitiveType.NullableBoolean:
                    if (br.ReadBoolean())
                    {
                        return br.ReadBoolean();
                    }
                    else return null;
                case PrimitiveType.NullableByte:
                    if (br.ReadBoolean())
                    {
                        return br.ReadByte();
                    }
                    else return null;
                case PrimitiveType.NullableChar:
                    if (br.ReadBoolean())
                    {
                        return br.ReadChar();
                    }
                    else return null;
                case PrimitiveType.NullableDecimal:
                    if (br.ReadBoolean())
                    {
                        return br.ReadDecimal();
                    }
                    else return null;
                case PrimitiveType.NullableDouble:
                    if (br.ReadBoolean())
                    {
                        return br.ReadDouble();
                    }
                    else return null;
                case PrimitiveType.NullableFloat:
                    if (br.ReadBoolean())
                    {
                        return br.ReadSingle();
                    }
                    else return null;
                case PrimitiveType.NullableInt:
                    if (br.ReadBoolean())
                    {
                        return br.ReadInt32();
                    }
                    else return null;
                case PrimitiveType.NullableLong:
                    if (br.ReadBoolean())
                    {
                        return br.ReadInt64();
                    }
                    else return null;
                case PrimitiveType.NullableSByte:
                    if (br.ReadBoolean())
                    {
                        return br.ReadSByte();
                    }
                    else return null;
                case PrimitiveType.NullableShort:
                    if (br.ReadBoolean())
                    {
                        return br.ReadInt16();
                    }
                    else return null;
                case PrimitiveType.NullableUInt:
                    if (br.ReadBoolean())
                    {
                        return br.ReadUInt32();
                    }
                    else return null;
                case PrimitiveType.NullableULong:
                    if (br.ReadBoolean())
                    {
                        return br.ReadUInt64();
                    }
                    else return null;
                case PrimitiveType.NullableUShort:
                    if (br.ReadBoolean())
                    {
                        return br.ReadUInt16();
                    }
                    else return null;
                case PrimitiveType.Enum:
                    {
                        var enumType = TypeHelper.GetType(br.ReadString());
                        var enumValue = PrimitiveFromBytes(br);
                        object value;
                        if (enumValue.TryCast(enumType, out value))
                        {
                            return value;
                        }
                        throw new NotSupportedException("The enum type specified is not supported");
                    }
                case PrimitiveType.NullableEnum:
                    {
                        /* 
                            bw.Write((int)PrimitiveType.NullableEnum);
                            bw.Write(Type.AssemblyQualifiedName);
                            var hasValue = value == null ? false : (bool)Type.GetProperty("HasValue").GetValue(value);
                            bw.Write(hasValue);
                            if (hasValue)
                            {
                                object cast;
                                object enumValue = Type.GetProperty("Value").GetValue(value);
                                if (enumValue.TryCast(Enum.GetUnderlyingType(Type.GetGenericArguments()[0]), out cast))
                                {
                                    PrimitiveToBytes(cast, cast.GetType(), bw);
                                    return;
                                }
                            } 
                        */

                        var enumType = TypeHelper.GetType(br.ReadString());
                        var hasValue = br.ReadBoolean();
                        var nullableEnum = Activator.CreateInstance(enumType);
                        if (hasValue)
                        {
                            var enumValue = PrimitiveFromBytes(br);
                            enumType.GetProperty("Value").SetValue(nullableEnum, enumValue);
                        }
                        return nullableEnum;
                    }
                default:
                    throw new NotSupportedException("The primitive type specified is not supported");
            }
        }
    }

    public enum PrimitiveType : int
    {
        String,
        DateTime,
        Boolean,
        Byte,
        SByte,
        Char,
        Short,
        Int,
        Long,
        UShort,
        UInt,
        ULong,
        Float,
        Double,
        Decimal,
        Enum,
        NullableDateTime,
        NullableBoolean,
        NullableByte,
        NullableSByte,
        NullableChar,
        NullableShort,
        NullableInt,
        NullableLong,
        NullableUShort,
        NullableUInt,
        NullableULong,
        NullableFloat,
        NullableDouble,
        NullableDecimal,
        NullableEnum
    }
}
