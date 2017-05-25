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
    public class PathNodeFilterMemberAccessExpression : BinarySerializableExpression
    {
        private Type nodeType;

        public PathNodeFilterMemberAccessExpression() { }
        public PathNodeFilterMemberAccessExpression(Type nodeType, Expression predicate)
        {
            this.nodeType = nodeType;
            this.Predicate = predicate;
        }

        public Expression Predicate { get; private set; }

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
                return (ExpressionType)QueryExpressionType.PathNodeFilterMember;
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
        public Type ModelType { get { return nodeType; } }


        protected override void OnToBytes(BinaryWriter bw)
        {
            bw.Write(ModelTypeManager.GetModelName(nodeType));
            var bytes = ((IBinarySerializable)Predicate).ToBytes();
            bw.Write((int)Predicate.NodeType);
            bw.Write(bytes.Length);
            bw.Write(bytes);
        }

        protected override void OnFromBytes(BinaryReader br)
        {
            nodeType = ModelTypeManager.GetModelType(br.ReadString());
            Predicate = CreateExpression((QueryExpressionType)br.ReadInt32());
            ((IBinarySerializable)Predicate).FromBytes(br.ReadBytes(br.ReadInt32()));
        }
    }
}
