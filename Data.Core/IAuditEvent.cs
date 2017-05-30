using Altus.Suffūz.Serialization.Binary;
using Common.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core
{
    public enum AuditEventType
    {
        Read,
        Create,
        Update,
        Delete
    }

    [Model("audit")]
    public interface IAuditEvent : ILink<long>
    {
        [BinarySerializable(10)]
        AuditEventType AuditEventType { get; set; }
        [BinarySerializable(11)]
        string AdditionalData { get; set; }
        [Searchable]
        [BinarySerializable(12)]
        string ScopeId { get; set; }
    }
}
