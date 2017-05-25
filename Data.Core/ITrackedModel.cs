using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core
{
    public interface ITrackedModel : IModel 
    {
        string TrackingKey { get; set; }
        IRepository Repository { get; }
        IModel Current { get; }
        IModel Committed { get; }
        void Revert();
        void Commit();
        void Commit(IModel model, bool retainDynamicMembers);
        TrackingState State { get; set; }
        bool IsObserving { get; }
        IList<string> WorkContextKeys { get; }
        

        TrackingState CalculateState(bool forceEvaluation = false);
    }
}
