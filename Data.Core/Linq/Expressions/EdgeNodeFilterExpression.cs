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
    public enum EdgeSelectionType
    {
        Inclusive,
        Exclusive,
        OptionalInclusive
    }
    public abstract class EdgeNodeFilterExpression : BinarySerializableExpression
    {
        private Type edgeType;
        private Type nodeType;

        protected EdgeNodeFilterExpression() { }

        public EdgeNodeFilterExpression(EdgeSelectionType selectionType, Type edgeType, Type nodeType, Expression predicate, Expression parent)
        {
            this.edgeType = edgeType;
            this.nodeType = nodeType;
            this.Predicate = predicate;
            this.Parent = parent;
            this.EdgeSelectionType = selectionType;
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
        public override Type Type
        {
            get
            {
                return typeof(PathSelector<>).MakeGenericType(nodeType);
            }
        }

        public string ModelType
        {
            get
            {
                if (nodeType == null) return null;
                return ((ModelAttribute)nodeType.GetCustomAttributes(typeof(ModelAttribute), true).Single()).FullyQualifiedName;
            }
        }

        public string EdgeType
        {
            get
            {
                if (edgeType == null) return null;
                return ((ModelAttribute)edgeType.GetCustomAttributes(typeof(ModelAttribute), true).Single()).FullyQualifiedName;
            }
        }

        public Expression Parent { get; private set; }

        [JsonIgnore]
        public override bool CanReduce
        {
            get
            {
                return base.CanReduce;
            }
        }

        public EdgeSelectionType EdgeSelectionType { get; private set; }

        protected override void OnToBytes(BinaryWriter bw)
        {
            bw.Write((int)EdgeSelectionType);
            bw.Write(EdgeType);
            bw.Write(ModelType);

            var bytes = Predicate == null ? new byte[0] : ((IBinarySerializable)Predicate).ToBytes();
            bw.Write(bytes.Length);
            bw.Write(bytes);
            bw.Write(Predicate == null ? 0 : (int)Predicate.NodeType);

            bytes = Parent == null ? new byte[0] : ((IBinarySerializable)Parent).ToBytes();
            bw.Write(bytes.Length);
            bw.Write(bytes);
            bw.Write(Parent == null ? 0 : (int)Parent.NodeType);
        }

        protected override void OnFromBytes(BinaryReader br)
        {
            EdgeSelectionType = (EdgeSelectionType)br.ReadInt32();
            edgeType = ModelTypeManager.GetModelType(br.ReadString());
            nodeType = ModelTypeManager.GetModelType(br.ReadString());

            var predLen = br.ReadInt32();
            var predBytes = br.ReadBytes(predLen);
            var predType = br.ReadInt32();
            if (predLen > 0)
            {
                Predicate = CreateExpression((QueryExpressionType)predType);
                ((IBinarySerializable)Predicate).FromBytes(predBytes);
            }

            var parentLen = br.ReadInt32();
            var parentBytes = br.ReadBytes(parentLen);
            var parentType = br.ReadInt32();
            if (parentLen > 0)
            {
                Parent = CreateExpression((QueryExpressionType)parentType);
                ((IBinarySerializable)Parent).FromBytes(parentBytes);
            }
        }
    }
}
