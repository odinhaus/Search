using Data.Core.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Evaluation
{
    public interface IEditableRuntime : IRuntime
    {
        IUser Authenticate(string username, string password);
        T Create<T>(T model, IOrgUnit orgUnit) where T : IModel;
        T Update<T>(T model) where T : IModel;
        T Get<T>(T model) where T : IModel;
        int Delete<T>(T model) where T : IModel;

        // settings
        bool IsSecurityEnabled { get; set; }
        bool IsAuditingEnabled { get; set; }

    }
}
