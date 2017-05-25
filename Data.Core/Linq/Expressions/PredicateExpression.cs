using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Common.Serialization.Binary;

namespace Data.Core.Linq
{
    public class PredicateExpression : BinarySerializableExpression
    {
        public PredicateExpression() { }
        public PredicateExpression(Expression body)
        {
            this.Body = body;
        }

        public Expression Body { get; private set; }

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
                return (ExpressionType)QueryExpressionType.Predicate;
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
            return string.Format("{0}", Body.ToString());
        }

        protected override void OnToBytes(BinaryWriter bw)
        {
            bw.Write((int)Body.NodeType);
            var bytes = ((IBinarySerializable)Body).ToBytes();
            bw.Write(bytes.Length);
            bw.Write(bytes);
        }

        protected override void OnFromBytes(BinaryReader br)
        {
            Body = CreateExpression((QueryExpressionType)br.ReadInt32());
            ((IBinarySerializable)Body).FromBytes(br.ReadBytes(br.ReadInt32()));
        }
    }
}
