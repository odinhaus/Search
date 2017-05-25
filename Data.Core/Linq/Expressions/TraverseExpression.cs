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
    public class TraverseExpression : BinarySerializableExpression
    {
        private Type rootType;
        public TraverseExpression() { }
        public TraverseExpression(Type rootType, TraverseOriginExpression origin, EdgeNodeFilterExpression pathFilter, TraverseReturnsExpression pathSelector)
        {
            this.rootType = rootType;
            this.PathFilter = pathFilter;
            this.PathSelector = pathSelector;
            this.Origin = origin;
        }

        public EdgeNodeFilterExpression PathFilter { get; private set; }
        public TraverseReturnsExpression PathSelector { get; private set; }

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
                return (ExpressionType)QueryExpressionType.Traverse;
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

        public TraverseOriginExpression Origin { get; private set; }

        [JsonIgnore]
        public override bool CanReduce
        {
            get
            {
                return base.CanReduce;
            }
        }

        protected override void OnToBytes(BinaryWriter bw)
        {
            byte[] bytes = Origin.ToBytes();
            bw.Write(bytes.Length);
            bw.Write(bytes);

            bw.Write(PathFilter is OutEdgeNodeFilterExpression);
            bytes = PathFilter.ToBytes();
            bw.Write(bytes.Length);
            bw.Write(bytes);

            bw.Write(PathSelector != null);
            if (PathSelector != null)
            {
                bytes = PathSelector.ToBytes();
                bw.Write(bytes.Length);
                bw.Write(bytes);
            }
        }

        protected override void OnFromBytes(BinaryReader br)
        {
            Origin = new TraverseOriginExpression();
            Origin.FromBytes(br.ReadBytes(br.ReadInt32()));

            if (br.ReadBoolean())
                PathFilter = new OutEdgeNodeFilterExpression();
            else
                PathFilter = new InEdgeNodeFilterExpression();

            PathFilter.FromBytes(br.ReadBytes(br.ReadInt32()));
            if (br.ReadBoolean())
            {
                PathSelector = new TraverseReturnsExpression();
                PathSelector.FromBytes(br.ReadBytes(br.ReadInt32()));
            }
        }
    }
}
