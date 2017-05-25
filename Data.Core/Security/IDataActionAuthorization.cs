using Altus.Suffūz.Serialization.Binary;
using Common.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Security
{
    //public enum DataActionAuthorizationType
    //{
    //    Unrestricted    = 0,
    //    AnyUser         = 1,
    //    User            = 2,
    //    Role            = 3,
    //    Custom          = 4
    //}

    //[DataActionAuthorizeRole(DataActions.All, "Admin")]
    //[DataActionAuthorizeUser(DataActions.All, "Admin")]
    //[Model("DataActionAuthorization", "Security")]
    //[DataActionAuthorizeAnyUser(DataActions.Read)]
    //public interface IDataActionAuthorization : IModel<long>
    //{
    //    [BinarySerializable(10)]
    //    string  TargetModelType { get; set; }
    //    [BinarySerializable(11)]
    //    DataActionAuthorizationType DataActionAuthorizationType { get; set; }
    //    [BinarySerializable(12)]
    //    string  UserName { get; set; }
    //    [BinarySerializable(13)]
    //    string RoleName { get; set; }
    //    [BinarySerializable(14)]
    //    string CustomAuthorizationEvaluatorType { get; set; }
    //    [BinarySerializable(15)]
    //    DataActions DataActions { get; set; }
    //}
}
