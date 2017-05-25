using Altus.Suffūz.Serialization.Binary;
using Common.Collections;
using Common.Security;
using Data.Core.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Security
{
    [TypeConverter(typeof(ModelTypeConverter))]
    [Model("Rule")]
    public interface IRule : IModel<long>
    {
        [BinarySerializable(10)]
        [Searchable]
        string Name { get; set; }
        [BinarySerializable(11)]
        string Description { get; set; }
        [BinarySerializable(12)]
        Flock<IRulePolicy> Policies { get; set; }
        [BinarySerializable(13)]
        [Searchable]
        DateTime EffectiveStartDate { get; set; }
        [BinarySerializable(14)]
        [Searchable]
        DateTime EffectiveEndDate { get; set; }
        [BinarySerializable(15)]
        int Rank { get; set; }
        [BinarySerializable(15)]
        [Searchable]
        string TargetApp { get; set; }
    }

    public interface IRulePolicy : ISubModel
    {
        [BinarySerializable(10)]
        DataActions Actions { get; set; }
        [BinarySerializable(11)]
        Entitlement Entitlement { get; set; }
        [BinarySerializable(12)]
        string UserEvaluator { get; set; }
        [BinarySerializable(13)]
        string ModelEvaluator { get; set; }
        [BinarySerializable(14)]
        string[] ModelTypes { get; set; }
        [BinarySerializable(15)]
        string FailureMessage { get; set; }
        [BinarySerializable(16)]
        string PolicySelector { get; set; }
    }
}
