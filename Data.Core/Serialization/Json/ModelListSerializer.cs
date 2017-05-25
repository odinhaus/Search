using Altus.Suffūz.Serialization;
using Common.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
using Common;
using Common.IO;
using Newtonsoft.Json.Linq;
using System.Collections;

namespace Data.Core.Serialization.Json
{
    public class ModelListSerializer : JsonConverter, ISerializer<IModelList>
    {
        public bool IsScalar
        {
            get
            {
                return false;
            }
        }

        public int Priority
        {
            get
            {
                return 0;
            }
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType.Implements<IModelList>();
        }

        public IModelList Deserialize(Stream inputSource)
        {
            return (IModelList)Deserialize(StreamHelper.GetBytes(inputSource), null);
        }

        public IModelList Deserialize(byte[] source)
        {
            return (IModelList)Deserialize(source, null);
        }

        public object Deserialize(byte[] source, Type targetType)
        {
            using (var ms = new MemoryStream(source))
            using (var tr = new JsonTextReader(new StreamReader(ms)))
                return ReadJson(tr, targetType, null, null);
        }

        

        public byte[] Serialize(object source)
        {
            using (var ms = new MemoryStream())
            {
                using (var tw = new JsonTextWriter(new StreamWriter(ms)))
                    WriteJson(tw, source, null);
                return ms.ToArray();
            }
        }

        public byte[] Serialize(IModelList source)
        {
            return Serialize((object)source);
        }

        public void Serialize(IModelList source, Stream outputStream)
        {
            Common.IO.StreamHelper.Write(outputStream, Serialize(source));
        }

        public bool SupportsFormat(string format)
        {
            return StandardFormats.JSON.Equals(format);
        }

        public bool SupportsType(Type type)
        {
            return CanConvert(type);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var jObj = JObject.Load(reader);
            var offset = jObj.Property("Offset").Value.Value<long>();
            var totalRecords = jObj.Property("TotalRecords").Value.Value<long>();
            var pageSize = jObj.Property("PageSize").Value.Value<int>();
            JArray items = (JArray)jObj.Property("Items").Value;
            Type itemType = objectType.GetGenericArguments()[0];
            IList list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType));
            foreach(var item in items)
            {
                list.Add(item.ToString().FromJson(itemType));
            }
            return Activator.CreateInstance(typeof(ModelList<>).MakeGenericType(itemType), new object[] { list, offset, totalRecords, list.Count, pageSize });
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            var list = value as IModelList;
            writer.WritePropertyName("Offset");
            writer.WriteValue(list.Offset);
            writer.WritePropertyName("TotalRecords");
            writer.WriteValue(list.TotalRecords);
            writer.WritePropertyName("PageSize");
            writer.WriteValue(list.PageSize);
            writer.WritePropertyName("Items");
            writer.WriteStartArray();
            foreach(var item in list)
            {
                writer.WriteRawValue(item.ToJson());
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
    }
}
