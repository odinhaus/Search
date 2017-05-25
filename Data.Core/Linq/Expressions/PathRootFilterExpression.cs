using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Data.Core.Linq
{
    public class PathRootFilterExpression : BinarySerializableExpression
    {
        private Type rootType;

        public PathRootFilterExpression() { }
        public PathRootFilterExpression(Type rootType)
        {
            this.rootType = rootType;
        }

        [JsonIgnore]
        public Type ModelType { get { return rootType; } }


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
                return (ExpressionType)QueryExpressionType.PathRootFilter;
            }
        }

        [JsonIgnore]
        public override Type Type
        {
            get
            {
                return typeof(PathSelector<>).MakeGenericType(rootType);
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
            bw.Write(ModelTypeManager.GetModelName(rootType));
        }

        protected override void OnFromBytes(BinaryReader br)
        {
            rootType = ModelTypeManager.GetModelType(br.ReadString());
        }
    }
}
