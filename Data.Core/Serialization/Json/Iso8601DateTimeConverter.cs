using Altus.Suffūz.Serialization;
using Newtonsoft.Json;
using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Common.IO;

namespace Data.Core.Serialization.Json
{
    public class Iso8601DateTimeConverter : JsonConverter, ISerializer<DateTime>
    {
        public bool IsScalar
        {
            get
            {
                return true;
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
            return objectType == typeof(DateTime);
        }

        public DateTime Deserialize(Stream inputSource)
        {
            var text = new List<byte>();
            var b = (byte)inputSource.ReadByte();
            do
            {
                text.Add(b);
                b = (byte)inputSource.ReadByte();
            } while (b != 34 && b != 39 && inputSource.CanRead); // filter for ' or "
            text.Add(b);
            return Deserialize(text.ToArray());
        }

        public DateTime Deserialize(byte[] source)
        {
            using (var ms = new MemoryStream(source))
            using (var sr = new StreamReader(ms))
            using (var jr = new JsonTextReader(sr))
                return (DateTime)ReadJson(jr, typeof(DateTime), null, null);
        }

        public object Deserialize(byte[] source, Type targetType)
        {
            return Deserialize(source);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            DateTime? dt =  (DateTime?)reader.Value ?? new DateTime?();

            if (dt.HasValue)
            {
                if (dt.Value.Kind == DateTimeKind.Utc)
                {
                    dt = new DateTime(dt.Value.Year, dt.Value.Month, dt.Value.Day, dt.Value.Hour, dt.Value.Minute, dt.Value.Second, dt.Value.Millisecond, DateTimeKind.Local);
                }
                return dt;
            }
            return new DateTime?();
        }

        public byte[] Serialize(object source)
        {
            return Serialize((DateTime)source);
        }

        public byte[] Serialize(DateTime source)
        {
            var dt = source;
            if (dt.Kind == DateTimeKind.Unspecified)
            {
                dt = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, dt.Millisecond, DateTimeKind.Local);
            }
            if (dt.Kind == DateTimeKind.Local)
            {
                dt = dt.ToUniversalTime();
            }
            return UTF8Encoding.UTF8.GetBytes(string.Format("\"{0}\"", dt.ToISO8601()));
        }

        public void Serialize(DateTime source, Stream outputStream)
        {
            StreamHelper.Write(outputStream, Serialize(source));
        }

        public bool SupportsFormat(string format)
        {
            return format == StandardFormats.JSON;
        }

        public bool SupportsType(Type type)
        {
            return type == typeof(DateTime);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var dt = (DateTime)value;
            if (dt.Kind == DateTimeKind.Unspecified)
            {
                dt = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, dt.Millisecond, DateTimeKind.Local);
            }
            if (dt.Kind == DateTimeKind.Local)
            {
                dt = dt.ToUniversalTime();
            }
            writer.WriteRawValue(string.Format("\"{0}\"", dt.ToISO8601()));
        }
    }
}
