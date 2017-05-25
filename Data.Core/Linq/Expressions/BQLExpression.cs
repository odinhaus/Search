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
    public class BQLExpression : BinarySerializableExpression
    {
        private Type rootType;
        public BQLExpression() { }
        public BQLExpression(Type rootType, EdgeNodeFilterExpression pathFilter, TraverseReturnsExpression pathSelector)
        {
            this.rootType = rootType;
            this.PathFilter = pathFilter;
            this.PathSelector = pathSelector;
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
                return (ExpressionType)QueryExpressionType.BQL;
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

        protected override void OnToBytes(BinaryWriter bw)
        {
            bw.Write(PathFilter is OutEdgeNodeFilterExpression);
            var bytes = PathFilter.ToBytes();
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
