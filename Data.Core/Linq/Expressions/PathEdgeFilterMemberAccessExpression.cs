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
    public class PathEdgeFilterMemberAccessExpression : BinarySerializableExpression
    {
        private Type edgeType;
        public PathEdgeFilterMemberAccessExpression() { }
        public PathEdgeFilterMemberAccessExpression(Type edgeType, Expression predicate)
        {
            this.edgeType = edgeType;
            this.Predicate = predicate;
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
                return (ExpressionType)QueryExpressionType.PathEdgeFilterMember;
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

        public Expression Predicate { get; private set; }

        [JsonIgnore]
        public override bool CanReduce
        {
            get
            {
                return base.CanReduce;
            }
        }

        public Type EdgeType { get { return edgeType; } }

        protected override void OnToBytes(BinaryWriter bw)
        {
            bw.Write(ModelTypeManager.GetModelName(edgeType));
            var bytes = ((IBinarySerializable)Predicate).ToBytes();
            bw.Write((int)Predicate.NodeType);
            bw.Write(bytes.Length);
            bw.Write(bytes);
        }

        protected override void OnFromBytes(BinaryReader br)
        {
            edgeType = ModelTypeManager.GetModelType(br.ReadString());
            Predicate = CreateExpression((QueryExpressionType)br.ReadInt32());
            ((IBinarySerializable)Predicate).FromBytes(br.ReadBytes(br.ReadInt32()));
        }
    }
}
