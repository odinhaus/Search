using Newtonsoft.Json;
using Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Linq
{
    public abstract class FunctionExpression : BinarySerializableExpression
    {
        protected FunctionExpression() { }
        protected FunctionExpression(QueryExpressionType functionType, Type returnType, params BinarySerializableExpression[] args)
        {
            _nodeType = functionType;
            _returnType = returnType;
            this.Args = args;
        }


        [JsonProperty("NodeType")]
        public string TypeName
        {
            get
            {
                return ((QueryExpressionType)NodeType).ToString();
            }
        }

        QueryExpressionType _nodeType;
        [JsonIgnore]
        public override ExpressionType NodeType
        {
            get
            {
                return (ExpressionType)_nodeType;
            }
        }

        Type _returnType;
        [JsonIgnore]
        public override Type Type
        {
            get
            {
                return _returnType;
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

        public BinarySerializableExpression[] Args { get; private set; }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(this._nodeType.ToString());
            sb.Append("(");
            for (int i = 0; i < Args.Length; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                sb.Append(this.Args[i].ToString());
            }
            sb.Append(")");
            return sb.ToString();
        }

        protected override void OnToBytes(BinaryWriter bw)
        {
            bw.Write((int)_nodeType);
            var returnsModel = _returnType.Implements<IModel>();
            bw.Write(returnsModel);
            if (returnsModel)
            {
                bw.Write(ModelTypeManager.GetModelName(_returnType));
            }
            else
            {
                bw.Write(_returnType.AssemblyQualifiedName);
            }
            bw.Write(Args.Length);
            for(int i = 0; i < Args.Length; i++)
            {
                bw.Write((int)Args[i].NodeType);
                var bytes = Args[i].ToBytes();
                bw.Write(bytes.Length);
                bw.Write(bytes);
            }
        }

        protected override void OnFromBytes(BinaryReader br)
        {
            _nodeType = (QueryExpressionType)br.ReadInt32();
            var returnsModel = br.ReadBoolean();
            if (returnsModel)
            {
                _returnType = ModelTypeManager.GetModelType(br.ReadString());
            }
            else
            {
                _returnType = TypeHelper.GetType(br.ReadString());
            }
            var argCount = br.ReadInt32();
            Args = new BinarySerializableExpression[argCount];
            for (int i = 0; i < argCount; i++)
            {
                var expression = (BinarySerializableExpression)CreateExpression((QueryExpressionType)br.ReadInt32());
                expression.FromBytes(br.ReadBytes(br.ReadInt32()));
                Args[i] = expression;
            }
        }
    }
}
