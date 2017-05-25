using Newtonsoft.Json;
using Common.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Auditing
{
    public enum AuditChangeType
    {
        NewOrModified,
        Added,
        Removed
    }
    public class AuditedChange
    {
        public int ItemIndex { get; set; }
        public string PropertyName { get; set; }
        public AuditChangeType ChangeType { get; set; }
        public object Value { get; set; }

        [JsonIgnore]
        public string ValueJson
        {
            get
            {
                if (Value == null)
                {
                    return "null";
                }
                else
                {
                    return Value.ToJson();
                }
            }
        }
    }
}
