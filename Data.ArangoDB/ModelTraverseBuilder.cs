using System;
using System.Collections.Generic;
using Data.Core;
using Data.Core.Linq;
using System.Linq;
using Data.Core.Compilation;
using Common;

namespace Data.ArangoDB
{
    internal class ModelTraverseBuilder<T> where T : IModel
    {
        public ModelTraverseBuilder()
        {
        }

        internal IEnumerable<Path<T>> Create(List<Dictionary<string, object>> value)
        {
            foreach (var dict in value)
            {
                // each dictionary is a path in the result
                var path = new Path<T>();
                var modelKey = ModelTypeManager.GetModelName(typeof(T));

                if (dict.ContainsKey("edges"))
                {
                    // we have a path to return
                    var edges = ((IList<object>)dict["edges"]).OfType<Dictionary<string, object>>().ToArray();
                    var nodes = ((IList<object>)dict["vertices"]).OfType<Dictionary<string, object>>().ToArray();

                    var links = new List<ILink>();
                    var models = new List<IModel>();

                    foreach (var node in nodes)
                    {
                        var modelType = (string)node["ModelType"];
                        var model = RuntimeModelBuilder.CreateModelInstanceActivator(ModelTypeManager.GetModelType(modelType), typeof(ModelBase), true)(node);
                        if (modelType.Equals(modelKey))
                        {
                            path.Root = (T)model;
                        }
                        models.Add((IModel)model);
                    }

                    path.Nodes = models.ToArray();


                    for (var i = 0; i < edges.Count(); i++)
                    {
                        var edge = edges[i];
                        var modelType = (string)edge["ModelType"];
                        var fromType = (string)edge["SourceType"];
                        var toType = (string)edge["TargetType"];
                        var link = (ILink)RuntimeModelBuilder.CreateModelInstanceActivator(ModelTypeManager.GetModelType(modelType), typeof(LinkBase), true)(edge);
                        var node1 = models[i];
                        var node2 = models[i + 1];
                        if (edge["SourceType"].ToString().Equals(ModelTypeManager.GetModelName(node1.ModelType)))
                        {
                            link.From = node1;
                            link.To = node2;
                        }
                        else
                        {
                            link.From = node2;
                            link.To = node1;
                        }
                        links.Add(link);
                    }

                    path.Edges = links.ToArray();
                }
                else
                {
                    var modelTypeName = (string)dict["ModelType"];
                    var modelType = ModelTypeManager.GetModelType(modelTypeName);

                    if (modelType.Implements<ILink>())
                    {
                        path.Edges = new ILink[] { (ILink)RuntimeModelBuilder.CreateModelInstanceActivator(modelType, typeof(LinkBase), true)(dict) };
                        path.Nodes = new IModel[0];
                    }
                    else
                    {
                        path.Nodes = new IModel[] { (IModel)RuntimeModelBuilder.CreateModelInstanceActivator(modelType, typeof(ModelBase), true)(dict) };
                        path.Edges = new ILink[0];
                    }
                    
                }

                yield return path;
            }
        }
    }
}