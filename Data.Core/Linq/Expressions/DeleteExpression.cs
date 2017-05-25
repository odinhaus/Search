using Newtonsoft.Json;
using Common.Serialization;
using Common.Serialization.Binary;
using Data.Core.Compilation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Linq
{
    public class DeleteExpression : BinarySerializableExpression
    {
        public DeleteExpression() { }
        public DeleteExpression(IModel model)
        {
            this.Model = model;
            this._modelType = model.ModelType;
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
                return (ExpressionType)QueryExpressionType.Delete;
            }
        }

        public string ModelType
        {
            get { return ModelTypeManager.GetModelName(_modelType); }
        }

        Type _modelType;
        [JsonIgnore]
        public override Type Type
        {
            get
            {
                return _modelType;
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

        public IModel Model { get; private set; }

        public override string ToString()
        {
            return string.Format("{0}.Delete(\"{1}\"))", ModelType, Model?.ToJson() ?? "null");
        }

        protected override void OnToBytes(BinaryWriter bw)
        {
            bw.Write(ModelType);
            var bytes = ((IBinarySerializable)Model).ToBytes();
            bw.Write(bytes.Length);
            bw.Write(bytes);
        }

        protected override void OnFromBytes(BinaryReader br)
        {
            _modelType = ModelTypeManager.GetModelType(br.ReadString());
            Model = (IModel)RuntimeModelBuilder.CreateModelInstance(_modelType);
            ((IBinarySerializable)Model).FromBytes(br.ReadBytes(br.ReadInt32()));
        }
    }
}
