using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core
{
    [AttributeUsage(AttributeTargets.Interface)]
    public class ModelAttribute : Attribute
    {
        public ModelAttribute(string modelName, string modulePath = null, bool isPublic = true)
        {
            ModelName = modelName;
            ModulePath = modulePath;
            IsPublic = isPublic;
        }
        public string ModelName { get; private set; }
        public string ModulePath { get; private set; }
        public bool IsPublic { get; private set; }

        public string FullyQualifiedName
        {
            get
            {
                if (string.IsNullOrEmpty(ModulePath))
                {
                    return ModelName;
                }
                else
                {
                    return string.Format("{0}.{1}", ModulePath, ModelName);
                }
            }
        }
    }
}
