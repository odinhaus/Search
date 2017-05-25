using Data.Core.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Linq.Win
{
    public class ClientQueryPolicy : QueryPolicy
    {
        public const int DEFAULT_LIMIT = 1000; // no limit on returned rows
        public const bool LAZY_LOAD_COMPLEX_TYPES = true; // allow provider to generate custom types to lazy load complex type graphs
        public const bool USE_QUERY_CACHE = true; // allow provider to cache and reuse cached queries
        public const bool TRACK_CHANGES = true; // allow provider to track entity changes to be included in Save() operations
        public const bool RETURN_TRACKED_DELETES = false; // allow provider to omit tracked deleted items from result sets
        public const bool LOOK_FOR_INOTIFY_COLLECTION_CHANGED = false; // off by default

        public ClientQueryPolicy()
        {
            Limit = DEFAULT_LIMIT;
            DeferLoadComplexTypes = LAZY_LOAD_COMPLEX_TYPES;
            UseQueryCache = USE_QUERY_CACHE;
            TrackChanges = TRACK_CHANGES;
            ReturnTrackedDeletes = RETURN_TRACKED_DELETES;
            LookForINotifyCollectionChanged = LOOK_FOR_INOTIFY_COLLECTION_CHANGED;
        }

        public override QueryPolice CreatePolice(QueryTranslator translator)
        {
            return new ClientQueryPolice(this, translator);
        }
    }
}
