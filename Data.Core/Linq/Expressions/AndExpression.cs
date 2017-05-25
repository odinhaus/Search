using Newtonsoft.Json;
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
    public class AndExpression : BinarySerializableExpression
    {
        public AndExpression() { }
        public AndExpression(Expression left, Expression right)
        {
            this.Left = left;
            this.Right = right;
        }

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
                return (ExpressionType)QueryExpressionType.And;
            }
        }

        [JsonIgnore]
        public override Type Type
        {
            get
            {
                return typeof(bool);
            }
        }

        public Expression Left { get; private set; }
        public Expression Right { get; private set; }

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
            return string.Format("({0} AND {1})", Left.ToString(), Right.ToString());
        }

        protected override void OnToBytes(BinaryWriter bw)
        {
            bw.Write((int)Left.NodeType);
            var left = ((IBinarySerializable)Left).ToBytes();
            bw.Write(left.Length);
            bw.Write(left);

            bw.Write((int)Right.NodeType);
            var right = ((IBinarySerializable)Right).ToBytes();
            bw.Write(right.Length);
            bw.Write(right);
        }

        protected override void OnFromBytes(BinaryReader br)
        {
            var left = CreateExpression((QueryExpressionType)br.ReadInt32());
            ((IBinarySerializable)left).FromBytes(br.ReadBytes(br.ReadInt32()));
            this.Left = left;

            var right = CreateExpression((QueryExpressionType)br.ReadInt32());
            ((IBinarySerializable)right).FromBytes(br.ReadBytes(br.ReadInt32()));
            this.Right = right;
        }
    }
}
