using Common;
using Data.Core;
using Data.Core.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.ArangoDB
{
    public class ModelCollectionManager
    {
        public const string EdgeCollection = "Edge";
        public const string AuditCollection = "Audit";
        public const string AccessCollection = "Access";
        public static string GetCollectionName<T>() where T : IModel
        {
            return GetCollectionName(typeof(T));
        }

        public static string GetCollectionName(Type modelType)
        {
            if (modelType.Implements<ILink>())
            {
                if (modelType.Implements<IAuditEvent>())
                {
                    return AuditCollection;
                }
                else
                {
                    return EdgeCollection;
                }
            }
            else if (modelType.Implements<IPath>())
            {
                throw new NotSupportedException();
            }
            else
            {
                var attrib = GetModelAttribute(modelType);
                return attrib.FullyQualifiedName.Replace(".", "_");
            }
        }

        public static void SplitGlobalKey(string globalKey, out string collectionName, out string key)
        {
            var split = globalKey.Split('/');
            collectionName = GetCollectionName(ModelTypeManager.GetModelType(split[0].Replace("_", ".")));
            key = split[1];
        }

        private static ModelAttribute GetModelAttribute(Type modelType)
        {
            ModelAttribute attrib = null;
            do
            {
                attrib = modelType.GetCustomAttributes(typeof(ModelAttribute), false).FirstOrDefault() as ModelAttribute;
                modelType = modelType.BaseType;
            } while (attrib == null && modelType != typeof(object));
            return attrib;
        }

        public static string GetCollectionId(IModel n)
        {
            return n.GlobalKey().Replace(".", "_");
        }

        public static string GetIdFromGlobalKey(string globalModelKey)
        {
            var splits = globalModelKey.Split('/');
            return splits[0].Replace(".", "_") + "/" + splits[1];
        }
    }
}
