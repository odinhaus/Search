using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Linq
{
    public class TrackingEnumerator<T> : IEnumerable<T> where T : IModel
    {
        private IEnumerable<T> _enumerable;
        private ITrackingRepository _repository;

        public TrackingEnumerator(IEnumerable<T> enumerable, ITrackingRepository repository)
        {
            this._enumerable = enumerable;
            this._repository = repository;
        }


        public IEnumerable<T> Source { get { return _enumerable; } }
        public ITrackingRepository Repository { get { return _repository; } }

        public IEnumerator<T> GetEnumerator()
        {
            foreach (var item in Source)
            {
                if (this.Repository.Policy.TrackChanges)
                {
                    var tracked = this.Repository.Attach<T>(item);
                    if (!(tracked is ITrackedModel)
                        || (this.Repository.Policy.ReturnTrackedDeletes
                        || ((ITrackedModel)tracked).State != TrackingState.ShouldDelete)) // suppress items marked for delete
                    {
                        yield return tracked;
                    }
                }
                else yield return item; // just return it, we're not tracking
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
