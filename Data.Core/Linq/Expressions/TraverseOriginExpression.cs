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
    public class TraverseOriginExpression :  BinarySerializableExpression
    {
        private Type rootType;
        public TraverseOriginExpression() { }
        public TraverseOriginExpression(Type rootType, string key)
        {
            this.rootType = rootType;
            this.Key = key;
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
                return (ExpressionType)QueryExpressionType.TraverseOrigin;
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

        public string Key { get; set; }
        public string ModelType
        {
            get
            {
                return ((ModelAttribute)Type.GetCustomAttributes(typeof(ModelAttribute), true).Single()).FullyQualifiedName;
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
            bw.Write(ModelType);
            bw.Write(Key);
        }

        protected override void OnFromBytes(BinaryReader br)
        {
            rootType = ModelTypeManager.GetModelType(br.ReadString());
            Key = br.ReadString();
        }
    }
}
