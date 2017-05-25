using Altus.Suffūz.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Common;
using Common.IO;
using Common.Serialization;
using Data.Core.Compilation;
using Data.Core.ComponentModel;
using Data.Core.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Serialization.Json
{
    public class ModelSerializer : JsonConverter, IModelSerializer
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
            return objectType.Implements(typeof(IModel));
        }


        public byte[] Serialize(IModel source)
        {
            return Serialize((object)source);
        }

        public void Serialize(IModel source, Stream outputStream)
        {
            StreamHelper.Write(outputStream, Serialize(source));
        }

        IModel ISerializer<IModel>.Deserialize(byte[] source)
        {
            return (IModel)Deserialize(source, null);
        }

        IModel ISerializer<IModel>.Deserialize(Stream inputSource)
        {
            return (IModel)Deserialize(StreamHelper.GetBytes(inputSource), null);
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

        public bool SupportsFormat(string format)
        {
            return StandardFormats.JSON.Equals(format);
        }

        public bool SupportsType(Type type)
        {
            return type.Implements<IModel>();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var jObj = JObject.Load(reader);

            if (objectType.IsGenericType && objectType.GetGenericTypeDefinition().Equals(typeof(Path<>)))
            {
                var path = (IPath)Activator.CreateInstance(objectType);
                var root = jObj.Property("Root").Value.Type == JTokenType.Null ? null : (JObject)jObj.Property("Root").Value;
                if (root != null)
                {
                    var rootType = ModelTypeManager.GetModelType(root.Property("ModelType").Value.Value<string>());

                
                    var subObj = ReadJsonModelProperty(root, rootType, serializer);
                    path.Root = (IModel)subObj;
                }

                var edges = (JArray)jObj.Property("Edges").Value;
                var nodes = (JArray)jObj.Property("Nodes").Value;

                var edgeList = new List<ILink>();
                var nodeList = new List<IModel>();

                foreach (var edge in edges)
                {
                    var edgeType = ModelTypeManager.GetModelType(((JObject)edge).Property("ModelType").Value.Value<string>());
                    edgeList.Add((ILink)ReadJsonModelProperty((JObject)edge, edgeType, serializer));
                }

                path.Edges = edgeList.ToArray();

                foreach (var node in nodes)
                {
                    var nodeType = ModelTypeManager.GetModelType(((JObject)node).Property("ModelType").Value.Value<string>());
                    nodeList.Add((IModel)ReadJsonModelProperty((JObject)node, nodeType, serializer));
                }

                path.Edges = edgeList.ToArray();
                path.Nodes = nodeList.ToArray();
                return path;
            }
            else
            {

                var modelType = RuntimeModelBuilder.CreateModelType(objectType);
                var props = modelType.GetInterfaces().SelectMany(it => it.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                                     .Where(pi => pi.CanWrite)
                                     .ToArray();
                var model = RuntimeModelBuilder.CreateModelInstance(objectType, ModelTypeConverter.ModelBaseType);

                foreach (var prop in props)
                {
                    var attribs = prop.GetCustomAttributes(true);

                    try
                    {
                        // when the runtime type builder emits types, it internally wraps the base type properties with new properties
                        // so we need to look at the base type attributes to get the serialization attributes
                        var baseProp = modelType.BaseType.GetProperty(prop.Name);
                        if (baseProp != null)
                            attribs = attribs.Union(baseProp.GetCustomAttributes(true)).ToArray();
                    }
                    catch { }

                    if (attribs.OfType<JsonIgnoreAttribute>().Count() > 0) continue;

                    var jsonName = attribs.OfType<JsonPropertyAttribute>().FirstOrDefault()?.PropertyName ?? prop.Name;
                    var jProp = jObj.Property(jsonName);
                    if (jProp != null && jProp.HasValues && jProp.Value.Type != JTokenType.Null)
                    {
                        if (jsonName.Equals("ModelType"))
                        {
                            prop.SetValue(model, ModelTypeManager.GetModelType(jProp.Value.ToString()));
                        }
                        else
                        {
                            if (jProp.Value.Type == JTokenType.Object || jProp.Value.Type == JTokenType.Array)
                            {
                                if (prop.PropertyType.Implements<IModel>())
                                {
                                    prop.SetValue(model, 
                                        ReadJsonModelProperty((JObject)jProp.Value, 
                                                                ModelTypeManager.GetModelType(((JObject)jProp.Value).Property("ModelType").Value.Value<string>()), 
                                                                serializer));
                                }
                                else
                                {
                                    prop.SetValue(model, jProp.Value.ToObject(prop.PropertyType, serializer));
                                }
                            }
                            else
                            {
                                if (prop.PropertyType.Equals(((JValue)jProp.Value).Value.GetType()))
                                    prop.SetValue(model, ((JValue)jProp.Value).Value);
                                else if (prop.PropertyType.IsAssignableFrom(((JValue)jProp.Value).Value.GetType()))
                                    prop.SetValue(model, Cast(((JValue)jProp.Value).Value, prop.PropertyType));
                                else
                                {
                                    var converter = TypeDescriptor.GetConverter(((JValue)jProp.Value).Value);
                                    if (converter.CanConvertTo(prop.PropertyType))
                                    {
                                        prop.SetValue(model, converter.ConvertTo(((JValue)jProp.Value).Value, prop.PropertyType));
                                    }
                                    else
                                    {
                                        converter = TypeDescriptor.GetConverter(prop.PropertyType);
                                        if (converter.CanConvertFrom(typeof(string)))
                                        {
                                            prop.SetValue(model, converter.ConvertFrom(((JValue)jProp.Value).Value.ToString()));
                                        }
                                        else
                                        {
                                            // dont know what else do here, so just try to assign it, but will likely toss an exception
                                            prop.SetValue(model, ((JValue)jProp.Value).Value);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        if (!prop.PropertyType.IsValueType)
                        {
                            prop.SetValue(model, null);
                        }
                    }
                }
                return model;
            }
        }

        private object Cast(object value, Type propertyType)
        {
            var valueArg = Expression.Parameter(value.GetType());
            var cast = Expression.Convert(valueArg, propertyType);
            var funcType = typeof(Func<,>).MakeGenericType(value.GetType(), propertyType);
            var lambda = Expression.Lambda(funcType, cast, valueArg).Compile();
            return lambda.DynamicInvoke(value);
        }

        private object ReadJsonModelProperty(JObject jObj, Type objectType, JsonSerializer serializer)
        {
            using (var ms = new MemoryStream(SerializationContext.Instance.TextEncoding.GetBytes(jObj.ToString())))
            using (var tr = new JsonTextReader(new StreamReader(ms)))
                return ReadJson(tr, objectType, null, serializer);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            var props = ((IModel)value).ModelType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                                 .Union(((IModel)value).ModelType.GetInterfaces().SelectMany(it => it.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)))
                                 .Where(pi => pi.CanRead)
                                 .ToArray();

            foreach(var prop in props)
            {
                var attribs = prop.GetCustomAttributes(true);

                try
                {
                    // when the runtime type builder emits types, it internally wraps the base type properties with new properties
                    // so we need to look at the base type attributes to get the serialization attributes
                    var baseProp = value.GetType().BaseType.GetProperty(prop.Name);
                    attribs = attribs.Union(baseProp?.GetCustomAttributes(true) ?? new object[0]).ToArray();
                }
                catch { }

                if (attribs.OfType<JsonIgnoreAttribute>().Count() > 0) continue;

                var jsonName = attribs.OfType<JsonPropertyAttribute>().FirstOrDefault()?.PropertyName ?? prop.Name;

                writer.WritePropertyName(jsonName);
                if (jsonName.Equals("ModelType"))
                {
                    writer.WriteRawValue(string.Format("\"{0}\"", ModelTypeManager.GetModelName(((IModel)value).ModelType)));
                }
                else
                {
                    writer.WriteRawValue(prop.GetValue(value).ToJson());
                }
            }

            writer.WriteEndObject();
        }

    }
}
