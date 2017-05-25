using Data.Core;
using Data.Core.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using Data.Core.Compilation;

namespace Data.ArangoDB
{
    public class PathListBuilder<T> where T : IModel
    {
        public ModelList<T> Create(List<Dictionary<string, object>> arangoDocuments)
        {
            /* EXAMPLE JSON
             
            [
              {
                "Attributes": 0,
                "Created": "2017-01-19T15:17:26.2984753Z",
                "ExternalKey": "535474a5-521a-4b0f-8781-6766764220a2",
                "FullName": "SHSFS/some_customer/Checks/check_1201.jpg",
                "IsDeleted": false,
                "LastAccessTime": "2017-01-19T20:55:05.1615611Z",
                "Length": 39243,
                "MetaData": {
                  "Description": "Check: 1201",
                  "DocumentType": "Check",
                  "ExtraInfo": ""
                },
                "ModelType": "Document",
                "Modified": "2017-01-19T20:55:05.1615611Z",
                "Name": "check_1201.jpg",
                "Parent": "SHSFS/some_customer/Checks/",
                "_id": "Shs_Document/11132371",
                "_key": "11132371",
                "_rev": "11219845",
                "_Edges": [
                  {
                    "Created": "1/19/2017 9:22:10 AM",
                    "IsDeleted": false,
                    "ModelType": "System.supports",
                    "Modified": "1/19/2017 9:22:10 AM",
                    "SourceType": "Document",
                    "TargetType": "Financial.Check",
                    "_from": "Shs_Document/11132371",
                    "_id": "Edge/11137846",
                    "_key": "11137846",
                    "_rev": "11137846",
                    "_to": "Financial_Check/11137825",
                    "_Model": {
                      "_key": "11137825",
                      "_id": "Financial_Check/11137825",
                      "_rev": "11138433",
                      "Amount": 215.1,
                      "CheckDate": "0001-01-01T00:00:00",
                      "Created": "2017-01-19T09:22:10.8230421-06:00",
                      "Entity": "Dr. Spok",
                      "IsAllocated": true,
                      "IsDeleted": false,
                      "IsNew": false,
                      "ModelType": "Financial.Check",
                      "Modified": "2017-01-19T09:22:10.8230421-06:00",
                      "Number": 0,
                      "Payor": "aetna",
                      "ReceivedDate": "0001-01-01T00:00:00",
                      "SubmittedDate": "0001-01-01T00:00:00"
                    }
                  }
                ]
              }
            ]

            */

            var converter = new DocumentConverter(arangoDocuments);
            var paths = converter.ToList();
            var count = paths.Count;
            return new ModelList<T>(paths, 0, count, count, count);
        }

        class DocumentConverter : IEnumerable<T>
        {
            public DocumentConverter(List<Dictionary<string, object>> arangoDocuments)
            {
                this.Documents = arangoDocuments;
                if (typeof(T) == typeof(IAny))
                {
                    this.Activator = (dict) =>
                    {
                        if (dict == null)
                            return default(T);

                        var modelName = dict["ModelType"].ToString();
                        var modelType = ModelTypeManager.GetModelType(modelName);
                        return (T)RuntimeModelBuilder.CreateModelInstanceActivator(modelType)(dict);
                    };
                }
                else
                {
                    this.Activator = RuntimeModelBuilder.CreateModelInstanceActivator<T>();
                }
            }

            public Func<Dictionary<string, object>, T> Activator { get; private set; }
            public List<Dictionary<string, object>> Documents { get; private set; }


            public IEnumerator<T> GetEnumerator()
            {
                foreach (var doc in Documents)
                {
                    foreach(var path in  WalkPaths(doc))
                    {
                        yield return (T)(object)path;
                    }
                }
            }

            private IEnumerable<Path<T>> WalkPaths(Dictionary<string, object> doc)
            {
                var paths = new List<Path<T>>();
                var edgeList = new List<ILink>();
                var nodeList = new List<IModel>();

                WalkPath(doc, edgeList, nodeList, paths);
                return paths;
            }

            private void WalkPath(Dictionary<string, object> doc, List<ILink> edges, List<IModel> nodes, List<Path<T>> paths)
            {
                object edgeList;
                var node = Activator(doc);
                nodes.Add(node);
                if (edges.Count > 0)
                {
                    var lastEdge = edges.Last();
                    var lastNode = nodes[nodes.Count - 2];
                    if (((LinkBase)(object)lastEdge)._from.Equals(GetNodeId(lastNode)))
                    {
                        ((ILink)lastEdge).To = node;
                    }
                    else
                    {
                        ((ILink)lastEdge).From = node;
                    }
                }
                if (doc != null && doc.TryGetValue("_edges", out edgeList) && ((List<object>)edgeList).Count > 0)
                {
                    foreach(var edgeDoc in ((List<object>)edgeList).OfType<Dictionary<string, object>>())
                    {
                        var edge = Activator(edgeDoc);
                        edges.Add((ILink)edge);

                        var nodeDoc = (Dictionary<string, object>)edgeDoc["_model"];

                        if (((LinkBase)(object)edge)._from.Equals(GetNodeId(node)))
                        {
                            ((ILink)edge).From = node;
                        }
                        else
                        {
                            ((ILink)edge).To = node;
                        }
                        WalkPath(nodeDoc, edges, nodes, paths);
                        // remove last edge before loopingback around
                        edges.RemoveAt(edges.Count - 1);
                    }
                }
                else
                {
                    // we hit the end of the path, add it to the list
                    paths.Add(new Path<T>()
                    {
                        Root = (T)nodes.First(),
                        Edges = edges.ToArray(),
                        Nodes = nodes.ToArray()
                    });
                    // remove last node before moving back up the recursion stack
                    nodes.RemoveAt(nodes.Count - 1);
                }
            }

            private string GetNodeId(IModel model)
            {
                return string.Format("{0}/{1}", ModelTypeManager.GetModelName(model.ModelType).Replace(".", "_"), model.GetKey());
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }
        }
    }
}
