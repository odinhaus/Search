using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Linq
{
    public class GTExpression : BinarySerializableExpression, IBinaryExpression
    {
        public GTExpression() { }
        public GTExpression(string member, BinarySerializableExpression value)
        {
            this.MemberName = member;
            this.Value = value;
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
                return (ExpressionType)QueryExpressionType.GT;
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

        public string MemberName { get; private set; }
        public BinarySerializableExpression Value { get; private set; }

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
            return string.Format("({0} > {1})", MemberName.ToString(), Value.ToString());
        }

        protected override void OnToBytes(BinaryWriter bw)
        {
            bw.Write(MemberName);
            var bytes = Value.ToBytes();
            bw.Write((int)Value.NodeType);
            bw.Write(bytes.Length);
            bw.Write(bytes);
        }

        protected override void OnFromBytes(BinaryReader br)
        {
            MemberName = br.ReadString();
            Value = CreateExpression((QueryExpressionType)br.ReadInt32());
            Value.FromBytes(br.ReadBytes(br.ReadInt32()));
        }
    }
}
