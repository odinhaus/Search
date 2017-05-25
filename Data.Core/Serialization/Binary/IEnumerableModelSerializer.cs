using Common.Serialization;
using Data.Core.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Common.DI;
using Altus.Suffūz.Serialization;
using Common;
using System.IO;
using System.Collections;
using Common.Serialization.Binary;

namespace Data.Core.Serialization.Binary
{
    public class IEnumerableModelSerializer : ISerializer
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

        public object Deserialize(byte[] source, Type targetType)
        {
            using (var ms = new MemoryStream(source))
            {
                using (var br = new BinaryReader(ms))
                {
                    var count = br.ReadInt32();
                    var isPath = br.ReadBoolean();
                    var modelType = ModelTypeManager.GetModelType(br.ReadString());
                    
                    var itemType = isPath ?
                        typeof(Path<>).MakeGenericType(modelType) :
                        modelType;
                    var listType = typeof(List<>).MakeGenericType(itemType);
                    var list = (IList)Activator.CreateInstance(listType);

                    for(int i = 0; i < count; i++)
                    {
                        var bytes = br.ReadBytes(br.ReadInt32());
                        var item = Activator.CreateInstance(itemType) as IBinarySerializable;
                        item.FromBytes(bytes);
                        list.Add(item);
                    }

                    return list;
                }
            }
        }

        public byte[] Serialize(object source)
        {
            using (var ms = new MemoryStream())
            {
                using (var bw = new BinaryWriter(ms))
                {
                    bw.Write(0); // we dont know the count yet, but we need to reserve the space for it
                    var sourceType = source.GetType();
                    Type modelType;

                    if (sourceType.IsArray)
                    {
                        modelType = sourceType.GetElementType();
                    }
                    else
                    {
                        modelType = sourceType.GetGenericArguments()[0];
                    }

                    var isPath = modelType.IsGenericType && modelType.GetGenericTypeDefinition().Equals(typeof(Path<>));
                    if (isPath)
                    {
                        bw.Write(true);
                        bw.Write(ModelTypeManager.GetModelName(modelType.GetGenericArguments()[0]));
                    }
                    else
                    {
                        bw.Write(false);
                        bw.Write(ModelTypeManager.GetModelName(modelType));
                    }

                    

                    var en = ((IEnumerable)source).GetEnumerator();
                    var count = 0;
                    while(en.MoveNext())
                    {
                        var bytes = ((IBinarySerializable)en.Current).ToBytes();
                        bw.Write(bytes.Length);
                        bw.Write(bytes);
                        count++;
                    }

                    ms.Position = 0;
                    bw.Write(count);

                    return ms.ToArray();
                }
            }
        }

        public bool SupportsFormat(string format)
        {
            return format.Equals(StandardFormats.BINARY);
        }

        public bool SupportsType(Type type)
        {
            return 
                (
                    type.Implements(typeof(IEnumerable<>))
                    && type.IsGenericType
                    && type.GetGenericArguments()[0].Implements<IModel>()
                )
                || 
                (
                    type.IsArray && 
                    type.GetElementType().IsGenericType && 
                    type.GetElementType().GetGenericArguments()[0].Implements<IModel>()
                );
        }
    }
}
