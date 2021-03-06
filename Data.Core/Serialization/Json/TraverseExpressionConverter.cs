﻿using Altus.Suffūz.Serialization;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Shs.Common.Application;
using Shs.Common.DI;
using Shs.Data.Core.Linq;
using Newtonsoft.Json.Linq;

namespace Shs.Data.Core.Serialization.Json
{
    public class TraverseExpressionConverter : JsonConverter, ISerializer<TraverseExpression>, IInitialize
    {
        TraverseExpressionSerializer _serializer = new TraverseExpressionSerializer();


        public bool IsScalar
        {
            get
            {
                return _serializer.IsScalar;
            }
        }

        public int Priority
        {
            get
            {
                return _serializer.Priority;
            }
        }

        public bool IsInitialized
        {
            get;
            private set;
        }

        public bool IsRegistered
        {
            get;
            private set;
        }

        public bool IsEnabled
        {
            get
            {
                return true;
            }
        }

        public override bool CanConvert(Type objectType)
        {
            return _serializer.SupportsType(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            return _serializer.Deserialize(JObject.Load(reader));
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var jsonBytes = Serialize(value);
            writer.WriteRawValue(SerializationContext.Instance.TextEncoding.GetString(jsonBytes));
        }

        public TraverseExpression Deserialize(Stream inputSource)
        {
            return _serializer.Deserialize(inputSource);
        }

        public TraverseExpression Deserialize(byte[] source)
        {
            return _serializer.Deserialize(source);
        }

        public object Deserialize(byte[] source, Type targetType)
        {
            return _serializer.Deserialize(source, targetType);
        }



        public byte[] Serialize(object source)
        {
            return _serializer.Serialize(source);
        }

        public byte[] Serialize(TraverseExpression source)
        {
            return _serializer.Serialize(source);
        }

        public void Serialize(TraverseExpression source, Stream outputStream)
        {
            _serializer.Serialize(source, outputStream);
        }

        public bool SupportsFormat(string format)
        {
            return _serializer.SupportsFormat(format);
        }

        public bool SupportsType(Type type)
        {
            return _serializer.SupportsType(type);
        }

        public void Initialize(string name, params string[] args)
        {
            IsInitialized = true;
        }

        public void Register(IContainerMappings containerMappings)
        {
            containerMappings.Add().Map<ISerializer<TraverseExpression>, TraverseExpressionConverter>();
            IsRegistered = true;
        }
    }
}
