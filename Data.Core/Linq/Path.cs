using Common.Serialization.Binary;
using Data.Core.Compilation;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Data.Core.Auditing;
using System.Linq.Expressions;

namespace Data.Core.Linq
{
    public class Path<T> : IModel, IPath<T>, IBinarySerializable where T : IModel
    {
        public Path()
        {
            ProtocolBuffer = new byte[0];
        }
        public T Root { get; set; }
        public ILink[] Edges { get; set; }
        public IModel[] Nodes { get; set; }

        public Type ModelType
        {
            get
            {
                return this.GetType();
            }
        }

        public bool IsDeleted
        {
            get
            {
                return false;
            }

            set
            {

            }
        }

        IModel IPath.Root
        {
            get
            {
                return Root;
            }

            set
            {
                Root = (T)value;
            }
        }

        public bool IsNew
        {
            get
            {
                return false;
            }
        }

        public DateTime Created
        {
            get { return Root.Created; }
            set { Root.Created = value; }
        }
        public DateTime Modified
        {
            get { return Root.Modified; }
            set { Root.Modified = value; }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public event PropertyChangingEventHandler PropertyChanging;

        public string GetKey()
        {
            return Root.GetKey();
        }

        public void SetKey(string value)
        {
            throw new NotImplementedException();
        }


        public byte[] ProtocolBuffer { get; set; }

        public byte[] ToBytes()
        {
            using (var ms = new MemoryStream())
            {
                using (var bw = new BinaryWriter(ms))
                {
                    bw.Write(Root != null);
                    if (Root != null)
                    {
                        var rootBytes = ((IBinarySerializable)Root).ToBytes();
                        bw.Write(ModelTypeManager.GetModelName(Root.ModelType));
                        bw.Write(rootBytes.Length);
                        bw.Write(rootBytes);
                    }

                    bw.Write(Edges?.Length ?? 0);
                    if (Edges != null && Edges.Length > 0)
                    {
                        for(int i = 0; i < Edges.Length; i++)
                        {
                            var edgeBytes = ((IBinarySerializable)Edges[i]).ToBytes();
                            bw.Write(ModelTypeManager.GetModelName(Edges[i].ModelType));
                            bw.Write(edgeBytes.Length);
                            bw.Write(edgeBytes);
                        }
                    }

                    bw.Write(Nodes?.Length ?? 0);
                    if (Nodes != null && Nodes.Length > 0)
                    {
                        for (int i = 0; i < Nodes.Length; i++)
                        {
                            var nodeBytes = ((IBinarySerializable)Nodes[i]).ToBytes();
                            bw.Write(ModelTypeManager.GetModelName(Nodes[i].ModelType));
                            bw.Write(nodeBytes.Length);
                            bw.Write(nodeBytes);
                        }
                    }

                    bw.Write(ProtocolBuffer.Length);
                    bw.Write(ProtocolBuffer);
                    return ms.ToArray();
                }
            }
        }

        public void FromBytes(byte[] source)
        {
            using (var ms = new MemoryStream(source))
            {
                using (var br = new BinaryReader(ms))
                {
                    if (br.ReadBoolean())
                    {
                        var rootType = ModelTypeManager.GetModelType(br.ReadString());
                        var rootLen = br.ReadInt32();
                        var rootBytes = br.ReadBytes(rootLen);
                        this.Root = (T)RuntimeModelBuilder.CreateModelInstance(rootType);
                        ((IBinarySerializable)this.Root).FromBytes(rootBytes);
                    }

                    var edgeCount = br.ReadInt32();
                    if (edgeCount > 0)
                    {
                        this.Edges = new ILink[edgeCount];
                        for(int i = 0; i < edgeCount; i++)
                        {
                            var edgeType = ModelTypeManager.GetModelType(br.ReadString());
                            var edgeLen = br.ReadInt32();
                            var edgeBytes = br.ReadBytes(edgeLen);
                            this.Edges[i] = (ILink)RuntimeModelBuilder.CreateModelInstance(edgeType);
                            ((IBinarySerializable)this.Edges[i]).FromBytes(edgeBytes);
                        }
                    }
                    else
                    {
                        Edges = new ILink[0];
                    }

                    var nodeCount = br.ReadInt32();
                    if (nodeCount > 0)
                    {
                        this.Nodes = new IModel[nodeCount];
                        for (int i = 0; i < nodeCount; i++)
                        {
                            var nodeType = ModelTypeManager.GetModelType(br.ReadString());
                            var nodeLen = br.ReadInt32();
                            var nodeBytes = br.ReadBytes(nodeLen);
                            this.Nodes[i] = (IModel)RuntimeModelBuilder.CreateModelInstance(nodeType);
                            ((IBinarySerializable)this.Nodes[i]).FromBytes(nodeBytes);
                        }
                    }
                    else
                    {
                        this.Nodes = new IModel[0];
                    }

                    this.ProtocolBuffer = br.ReadBytes(br.ReadInt32());
                }
            }
        }

        public IEnumerable<AuditedChange> Compare(IModel model, string prefix)
        {
            throw new NotSupportedException();
        }

        public static explicit operator T(Path<T> path)
        {
            return path.Root;
        }

    }

    public class Path
    {
        static Dictionary<Type, Delegate> _initializers = new Dictionary<Type, Delegate>();
        public static IPath Create(Type rootType, IModel root, IModel[] nodes, ILink[] edges)
        {
            Delegate init;
            lock (_initializers)
            {
                if (!_initializers.TryGetValue(rootType, out init))
                {
                    var pathType = typeof(Path<>).MakeGenericType(rootType);
                    var initializer = Expression.New(pathType.GetConstructor(Type.EmptyTypes));
                    var pathVar = Expression.Variable(pathType);
                    var assignVar = Expression.Assign(pathVar, initializer);

                    var rootParam = Expression.Parameter(rootType);
                    var nodesParam = Expression.Parameter(typeof(IModel[]));
                    var edgesParam = Expression.Parameter(typeof(ILink[]));

                    var rootSetter = Expression.Call(pathVar, pathType.GetProperty("Root").SetMethod, rootParam);
                    var nodesSetter = Expression.Call(pathVar, pathType.GetProperty("Nodes").SetMethod, nodesParam);
                    var edgesSetter = Expression.Call(pathVar, pathType.GetProperty("Edges").SetMethod, edgesParam);

                    var returnLabel = Expression.Label(pathType, "return");
                    var result = Expression.Return(returnLabel, pathVar);
                    var end = Expression.Label(returnLabel, Expression.Constant(null, pathType));

                    var body = Expression.Block(new ParameterExpression[] { pathVar },
                        assignVar,
                        rootSetter,
                        nodesSetter,
                        edgesSetter,
                        result,
                        end);

                    var initExp = Expression.Lambda(body, rootParam, nodesParam, edgesParam);
                    init = initExp.Compile();
                    _initializers.Add(rootType, init);
                }
            }
            return (IPath)init.DynamicInvoke(root, nodes, edges);
        }

        public static IPath<T> Create<T>(T root, IModel[] nodes, ILink[] edges) where T : IModel
        {
            return (IPath<T>)Create(typeof(T), root, nodes, edges);
        }
    }
}
