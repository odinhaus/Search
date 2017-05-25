using Newtonsoft.Json;
using Common.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Common.Serialization.Binary;
using Data.Core.Compilation;

namespace Data.Core.Linq
{
    public class SaveExpression : BinarySerializableExpression
    {
        public SaveExpression() { }
        public SaveExpression(IModel model, IOrgUnit owner)
        {
            this.Model = model;
            this.Owner = owner;
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
                return (ExpressionType)QueryExpressionType.Save;
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
        public IOrgUnit Owner { get; private set; }

        public override string ToString()
        {
            return string.Format("{0}.Save(\"{1}\"))", ModelType, Model?.ToJson() ?? "null");
        }

        protected override void OnToBytes(BinaryWriter bw)
        {
            bw.Write(ModelType);
            var bytes = ((IBinarySerializable)Model).ToBytes();
            bw.Write(bytes.Length);
            bw.Write(bytes);
            bytes = ((IBinarySerializable)Owner)?.ToBytes() ?? new byte[0];
            bw.Write(bytes.Length);
            bw.Write(bytes);
        }

        protected override void OnFromBytes(BinaryReader br)
        {
            _modelType = ModelTypeManager.GetModelType(br.ReadString());
            Model = (IModel)RuntimeModelBuilder.CreateModelInstance(_modelType);
            var bytes = br.ReadBytes(br.ReadInt32());
            ((IBinarySerializable)Model).FromBytes(bytes);
            bytes = br.ReadBytes(br.ReadInt32());
            if (bytes.Length > 0)
            {
                Owner = RuntimeModelBuilder.CreateModelInstance<IOrgUnit>();
                ((IBinarySerializable)Owner).FromBytes(bytes);
            }
        }
    }
}
