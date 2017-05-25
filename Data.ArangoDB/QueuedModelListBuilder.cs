using Data.Core;
using Data.Core.Compilation;
using Data.Core.Domains.Operations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.ArangoDB
{
    public class QueuedModelListBuilder<T> where T : IModel
    {
        public ModelList<QueuedModel<T>> Create(List<Dictionary<string, object>> arangoDocuments)
        {
            var converter = new DocumentConverter(arangoDocuments);
            var paths = converter.ToList();
            var count = paths.Count;
            return new ModelList<QueuedModel<T>>(paths, 0, count, count, count);
        }

        class DocumentConverter : IEnumerable<QueuedModel<T>>
        {
            public DocumentConverter(List<Dictionary<string, object>> arangoDocuments)
            {
                this.Documents = arangoDocuments;

                this.Activator = (dict) =>
                {
                    var modelName = dict["ModelType"].ToString();
                    var modelType = ModelTypeManager.GetModelType(modelName);
                    return (T)RuntimeModelBuilder.CreateModelInstanceActivator(modelType)(dict);
                };
            }

            public Func<Dictionary<string, object>, T> Activator { get; private set; }
            public List<Dictionary<string, object>> Documents { get; private set; }

            public IEnumerator<QueuedModel<T>> GetEnumerator()
            {
                foreach (var doc in Documents)
                {
                    var root = Activator(doc);
                    var queue = doc["_queue"] as Dictionary<string, object>;

                    var item = new QueuedModel<T>(root, (int)(long)queue["Rank"]);
                    item.ForcedRank = (int)(long)queue["ForcedRank"];
                    item.IsOnHold = (bool)queue["IsOnHold"];

                    yield return item;
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }
        }
    }
}
