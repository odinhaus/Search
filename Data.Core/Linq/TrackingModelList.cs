using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Linq
{
    public class TrackingModelList<T> : ModelList<T> where T : IModel
    {
        private ITrackingRepository _repository;

        public TrackingModelList(ModelList<T> enumerable, ITrackingRepository repository)
            : base(enumerable, enumerable.Offset, enumerable.TotalRecords, enumerable.PageCount, enumerable.PageSize)
        {
            this._repository = repository;
            if (this.Repository.Policy.TrackChanges)
            {
                var deletes = new List<int>();
                for(int i = 0; i < enumerable.Count; i++)
                {
                    var item = enumerable[i];
                    var tracked = this.Repository.Attach<T>(item);
                    if (tracked == null || (tracked is ITrackedModel && ((ITrackedModel)tracked).State == TrackingState.ShouldDelete))
                        deletes.Add(i);
                    else
                        this[i] = tracked;
                }
                for(int i = deletes.Count - 1; i >= 0; i--)
                {
                    RemoveAt(deletes[i]);    
                }
            }
        }

        public ITrackingRepository Repository { get { return _repository; } }
    }
}
