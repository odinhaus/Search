using Newtonsoft.Json;
using Common;
using Common.Serialization.Binary;
using Data.Core.Compilation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Linq
{
    public class ScalarExpression : BinarySerializableExpression
    {
        public ScalarExpression() { }

        public ScalarExpression(object value, Type valueType)
        {
            this.Value = value;
            _valueType = valueType;
        }

        public object Value { get; private set; }

        [JsonProperty("NodeType")]
        public string TypeName
        {
            get
            {
                return ((QueryExpressionType)NodeType).ToString();
            }
        }

        [JsonIgnore]
        public override ExpressionType NodeType
        {
            get
            {
                return (ExpressionType)QueryExpressionType.Scalar;
            }
        }

        Type _valueType;
        [JsonIgnore]
        public override Type Type
        {
            get
            {
                return _valueType;
            }
        }

        [JsonIgnore]
        public override bool CanReduce
        {
            get
            {
                return base.CanReduce;
            }
        }

        public override string ToString()
        {
            return string.Format("{0}", Value?.ToString() ?? "null");
        }

        protected override void OnToBytes(BinaryWriter bw)
        {
            var returnsModel = _valueType.Implements<IModel>();
            bw.Write(returnsModel);
            if (returnsModel)
            {
                bw.Write(ModelTypeManager.GetModelName(((IModel)Value).ModelType));
            }
            else
            {
                bw.Write(_valueType.AssemblyQualifiedName);
            }

            if (Value is IBinarySerializable)
            {
                bw.Write(false);
                var bytes = ((IBinarySerializable)Value).ToBytes();
                bw.Write(bytes.Length);
                bw.Write(bytes);
            }
            else
            {
                bw.Write(true);
                PrimitiveToBytes(Value, Type, bw);
            }
        }

        protected override void OnFromBytes(BinaryReader br)
        {
            var returnsModel = br.ReadBoolean();
            if (returnsModel)
            {
                _valueType = ModelTypeManager.GetModelType(br.ReadString());
            }
            else
            {
                _valueType = TypeHelper.GetType(br.ReadString());
            }

            if (br.ReadBoolean())
            {
                Value = PrimitiveFromBytes(br);
            }
            else
            {
                IBinarySerializable value;
                if (_valueType.Implements<IModel>())
                {
                    value = RuntimeModelBuilder.CreateModelInstance(_valueType) as IBinarySerializable;
                }
                else
                {
                    value = Activator.CreateInstance(_valueType) as IBinarySerializable;
                }
                value.FromBytes(br.ReadBytes(br.ReadInt32()));
                Value = value;
            }
        }

        public static explicit operator ScalarExpression(ConstantExpression expression)
        {
            return new ScalarExpression(expression.Value, expression.Type);
        }
    }
}
