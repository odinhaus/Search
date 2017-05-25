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
    public enum ReturnsType
    {
        Model,
        Edge
    }
    public class TraverseReturnsExpression : BinarySerializableExpression
    {
        private Type rootType;
        public TraverseReturnsExpression() { }
        public TraverseReturnsExpression(Type rootType, int depth, ReturnsType returns )
        {
            this.rootType = rootType;
            this.ReturnsType = returns;
            this.Depth = depth;
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
                return (ExpressionType)QueryExpressionType.TraverseReturns;
            }
        }

        [JsonIgnore]
        public override Type Type
        {
            get
            {
                return rootType;
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

        public string ModelType
        {
            get
            {
                return ((ModelAttribute)Type.GetCustomAttributes(typeof(ModelAttribute), true).Single()).FullyQualifiedName;
            }
        }

        public ReturnsType ReturnsType { get; private set; }
        public int Depth { get; private set; }

        protected override void OnToBytes(BinaryWriter bw)
        {
            bw.Write(ModelType);
            bw.Write(Depth);
            bw.Write((int)ReturnsType);
        }

        protected override void OnFromBytes(BinaryReader br)
        {
            rootType = ModelTypeManager.GetModelType(br.ReadString());
            Depth = br.ReadInt32();
            ReturnsType = (ReturnsType)br.ReadInt32();
        }
    }
}
