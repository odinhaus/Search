using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using Data.Core.Linq;
using Common.Extensions;

namespace Data.Core
{
    public static class ModelTypeManager
    {
        static Dictionary<string, Type> _models = new Dictionary<string, Type>();
        static ModelTypeManager()
        {
            _models = new Dictionary<string, Type>();

            foreach (var asm in AppContext.Current?.Apps.SelectMany(app => app.Manifest.Targets)
                .SelectMany(target => target.Files)
                .Where(file => file.Reflect)
                .Select(file => file.LoadedAssembly) ?? AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm == null) continue;

                foreach (var type in asm.GetTypes())
                {
                    var modelAttrib = (ModelAttribute)type.GetCustomAttributes(typeof(ModelAttribute), true).FirstOrDefault();
                    if (modelAttrib == null) continue; // its not a service type or model type, so bail
                    _models.Add(modelAttrib.FullyQualifiedName, type);
                }
            }
        }

        public static string GetModelName<T>() where T : IModel
        {
            return GetModelName(typeof(T));
        }

        public static string GetModelName(Type type)
        {
            if (type.Implements<IPath>())
            {
                type = type.GetGenericArguments()[0];
                foreach (var kvp in _models)
                {
                    if (kvp.Value.Equals(type))
                        return "PathOf[" + kvp.Key + "]";
                }
            }
            else
            {
                foreach (var kvp in _models)
                {
                    if (kvp.Value.Equals(type))
                        return kvp.Key;
                }
            }
            return null;
        }

        public static Type GetModelType(string fullyQualifiedName)
        {
            if (fullyQualifiedName.StartsWith("PathOf["))
            {
                fullyQualifiedName = fullyQualifiedName.Remove(0, 7);
                fullyQualifiedName = fullyQualifiedName.Left("]");
                return typeof(Path<>).MakeGenericType(_models[fullyQualifiedName]);
            }
            else
            {
                return _models[fullyQualifiedName];
            }
        }

        public static Type GetModelType(Type runtimeType)
        {
            if (runtimeType.Implements<IModel>())
            {
                if (runtimeType.IsInterface) return runtimeType;
                return runtimeType.GetInterfaces().First(i => i.Implements<IModel>());
            }
            throw new InvalidOperationException("The runtimeType does not implement IModel");
        }

        public static bool TryGetModelType(string fullyQualifiedName, out Type modelType)
        {
            //return _models.TryGetValue(fullyQualifiedName, out modelType);

            if (fullyQualifiedName.StartsWith("PathOf["))
            {
                fullyQualifiedName = fullyQualifiedName.Remove(0, 7);
                fullyQualifiedName = fullyQualifiedName.Left("]");
                if (_models.TryGetValue(fullyQualifiedName, out modelType))
                {
                    modelType = typeof(Path<>).MakeGenericType(modelType);
                    return true;
                }
                return false;
            }
            else
            {
                return _models.TryGetValue(fullyQualifiedName, out modelType);
            }
        }

        public static IEnumerable<Type> ModelTypes
        {
            get
            {
                foreach (var type in _models)
                    yield return type.Value;
            }
        }

        public static IEnumerable<string> ModelFullNames
        {
            get
            {
                foreach (var type in _models)
                    yield return type.Key;
            }
        }

        public static IEnumerable<string> ModelNames
        {
            get
            {
                foreach (var type in _models)
                    yield return type.Key.Split('.').Last();
            }
        }

        public static IEnumerable<ModelMember> GetModelMembers<T>() where T : IModel
        {
            return GetModelMembers(typeof(T));
        }

        public static IEnumerable<ModelMember> GetModelMembers(Type modelType)
        {
            foreach(var prop in modelType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                var attribs = prop.GetCustomAttributes(true);
                yield return new ModelMember(prop.Name, attribs.Any(a => a is SearchableAttribute), attribs.Any(a => a is UniqueAttribute));
            }
        }
    }

    public class ModelMember
    {
        public ModelMember(string name, bool isSearchable, bool isUnique)
        {
            Name = name;
            IsSearchable = isSearchable;
            IsUnique = isUnique;
        }
        public string Name { get; private set; }
        public bool IsSearchable { get; private set; }
        public bool IsUnique { get; private set; }
    }
}
