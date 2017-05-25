using Common;
using Common.Extensions;
using Common.Security;
using Data.Core.Auditing;
using Data.Core.Compilation;
using Data.Core.Grammar;
using Data.Core.Linq;
using Data.Core.Evaluation;
using Data.Core.Security;
using Data.Core.Templating;
using Data.Core.Templating.Grammar;
using Data.Core.Web;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Data.Core.Scripting;

namespace Data.Core
{
    public class QueryOverride : Core.Linq.ExpressionVisitor, IOverride
    {
        public Argument[] ArgumentTypes
        {
            get
            {
                return new Argument[] { new Argument("projector", typeof(string)) };
            }
        }

        public MethodInfo MethodToCall { get; private set; }
        public Type ReturnType { get; private set; }
        public Type ModelType { get; private set; }

        public Expression CreateCall(Expression provider, MethodInfo method, Expression argumentsArray)
        {
            this.ReturnType = typeof(List<object>);
            this.MethodToCall = method;
            this.ModelType = method.DeclaringType.GetGenericArguments()[0];
            var queryMethod = typeof(QueryOverride).GetMethod("Query", BindingFlags.NonPublic | BindingFlags.Instance).MakeGenericMethod(ModelType);
            var args = new List<Expression>();
            args.Add(provider);
            args.AddRange(MapParameters(argumentsArray, queryMethod.GetParameters().Skip(1).ToArray()));
            return Expression.Call(Expression.Constant(this), queryMethod, args);
        }

        private IEnumerable<Expression> MapParameters(Expression args, ParameterInfo[] parms)
        {
            for (int p = 0; p < parms.Length; p++)
            {
                // read element p from the passed in IList<object> parameter, args.
                yield return Expression.Convert(Expression.ArrayAccess(args, Expression.Constant(p)), parms[p].ParameterType);
            }
        }

        static Dictionary<string, Func<string, List<object>>> _overrides = new Dictionary<string, Func<string, List<object>>>();
        private List<object> Query<T>(object provider, string query, string projector) where T : IModel
        {
            var checksum = projector.ToBase64MD5();
            Func<string, List<object>> func;
            lock (_overrides)
            {
                if (!_overrides.TryGetValue(checksum, out func))
                {
                    var modelType = ParseModelType(query);
                    var model = (modelType.Implements<IAny>()
                                    ? new Any()
                                    : modelType.Implements<IPath>()
                                        ? Activator.CreateInstance(modelType)
                                        : RuntimeModelBuilder.CreateModelInstance(modelType)) as IModel;
                    modelType = modelType.Implements<IPath>() ? ((IPath)model).Root.ModelType : model.ModelType;
                    var runtime = AppContext.Current.Container.GetInstance<IRuntimeBuilder>()
                                                              .Create(DataActions.Read, null, SecurityContext.Current.ToUser(), model, modelType, new AuditedChange[0]);
                    var codeContext = TemplateContext.Create(checksum, runtime.GetType(), modelType, checksum);
                    try
                    {
                        VariableScope.Current.Add("@@index", Expression.Parameter(typeof(int), "@@index"));
                        VariableScope.Current.Add("@@count", Expression.Parameter(typeof(int), "@@count"));
                        IEnumerable<DDV.DDVError> ddvErrors;
                        DDVParser.FunctionDefinitionContext ctx = null;
                        if (DDV.Validate(projector, (p) => { ctx = p.functionDefinition(); return ctx; }, out ddvErrors))
                        {
                            var projectorExp = codeContext.Visit(ctx);
                            if (projectorExp.Type.Name.StartsWith("Func")
                                && projectorExp.Type.GetGenericArguments().Length == 2
                                && !projectorExp.Type.GetGenericArguments()[0].Implements<IModel>())
                            {
                                var anonType = projectorExp.Type.GetGenericArguments()[1];
                                var queryMethod = MethodToCall;
                                var parms = new ParameterExpression[]
                                {
                                Expression.Parameter(typeof(string))
                                };

                                // this will get the results of the query from the provider Query(string query) method
                                // returns ModelList<IAny> where IAny is either a Path<T> or T
                                var callQuery = Expression.Call(Expression.Constant(provider), queryMethod, parms);

                                // the projector expression will likely reference an input type of Object as param 1$
                                // we need to rebuild the expression to use the IRuntime.Model property
                                _runtime = Expression.Parameter(typeof(IRuntime), "@runtime");
                                projectorExp = Visit(projectorExp); // produces Func<IRuntime, TResult>

                                // we need to call the projector on individual items in the resulting enumerable, updating @@index on each item
                                // so we need to build a foreach iterator code block, and loop over each result, calling the projector
                                var loopVar = Expression.Variable(modelType, "@item");
                                var projectionListVar = Expression.Variable(typeof(List<object>), "@results");
                                var projectionList = Expression.New(typeof(List<object>).GetConstructor(Type.EmptyTypes));
                                var newProjectionList = Expression.Assign(projectionListVar, projectionList);



                                var collectionVar = Expression.Variable(typeof(ModelList<IAny>));
                                var collectionAssign = Expression.Assign(collectionVar, callQuery);

                                // in order to call the projector, we need to create an instance of IRuntime, assigning the results from Query(string query) to the Model
                                var createRuntime = Expression.Call(null, typeof(QueryOverride)
                                                              .GetMethod("CreateRuntime", BindingFlags.NonPublic | BindingFlags.Static), Expression.Convert(loopVar, typeof(IAny)));

                                // now we need to Invoke the Func<IRuntime, TResult> projectorExp, passing in the created IRuntime
                                var invokeProjector = Expression.Invoke(projectorExp, createRuntime);
                                // add the projected item to the results list
                                var addProjected = Expression.Call(projectionListVar, typeof(List<object>).GetMethod("Add"), invokeProjector);

                                var body = Expression.Block(new ParameterExpression[] { (ParameterExpression)_runtime },
                                    addProjected);

                                var castMethod = typeof(Invoker).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                                                     .Single(mi => mi.Name.Equals("Cast") && mi.GetParameters()[0].ParameterType.Equals(typeof(IEnumerable)))
                                                                     .MakeGenericMethod(modelType);
                                var typedCollection = Expression.Call(null, castMethod, collectionVar);

                                var loop = ForEach(typedCollection, loopVar, body, VariableScope.Search("@@index"), VariableScope.Search("@@count"));

                                var returnLabel = Expression.Label(typeof(List<object>), "return");

                                var block = Expression.Block(new ParameterExpression[] { projectionListVar, collectionVar },
                                    collectionAssign,
                                    newProjectionList,
                                    loop,
                                    Expression.Return(returnLabel, projectionListVar),
                                    Expression.Label(returnLabel, Expression.Constant(new List<object>())));

                                // lastly, compile this as Func, and return the created anonymous type
                                var projectorFinal = Expression.Lambda<Func<string, List<object>>>(block, parms);
                                func = projectorFinal.Compile();
                            }
                        }
                        else
                        {
                            func = null;
                        }
                        _overrides.Add(checksum, func);
                    }
                    finally
                    {
                        VariableScope.Clear();
                    }
                }
            }

            if (func == null)
                throw new InvalidOperationException("The projector expression is invalid.");
            else
                return func(query);
        }

        private Expression ForEach(Expression collection, ParameterExpression loopVar, Expression body, Expression index, Expression count)
        {
            /* EXAMPLE USAGE
                var collection = Expression.Parameter(typeof(List<string>), "collection");
                var loopVar = Expression.Parameter(typeof(string), "loopVar");
                var loopBody = Expression.Call(typeof(Console).GetMethod("WriteLine", new[] { typeof(string) }), loopVar);
                var loop = ForEach(collection, loopVar, loopBody);
                var compiled = Expression.Lambda<Action<List<string>>>(loop, collection).Compile();
                compiled(new List<string>() { "a", "b", "c" });

                foreach(loopvar in collection)
                {
                    body
                }
            */

            var elementType = loopVar.Type;
            var enumerableType = typeof(IEnumerable<>).MakeGenericType(elementType);
            var enumeratorType = typeof(IEnumerator<>).MakeGenericType(elementType);

            var enumeratorVar = Expression.Variable(enumeratorType, "enumerator");

            var getEnumeratorCall = Expression.Call(collection, enumerableType.GetMethod("GetEnumerator"));
            var enumeratorAssign = Expression.Assign(enumeratorVar, getEnumeratorCall);

            // The MoveNext method's actually on IEnumerator, not IEnumerator<T>
            var moveNextCall = Expression.Call(enumeratorVar, typeof(IEnumerator).GetMethod("MoveNext"));

            var breakLabel = Expression.Label("LoopBreak");

            // making @@index and @@count available in ForEach loops
            var indexVar = (ParameterExpression)index;
            var lengthVar = (ParameterExpression)count;
            var indexInit = Expression.Assign(indexVar, Expression.Constant(0));
            var lengthInit = Expression.Assign(
                lengthVar,
                Expression.Call(
                    null,
                    typeof(Enumerable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                        .Single(mi => mi.Name.Equals("Count") && mi.GetParameters().Length == 1)
                                        .MakeGenericMethod(elementType),
                    collection));
            var indexIncrement = Expression.Assign(indexVar, Expression.Add(indexVar, Expression.Constant(1)));

            var loop = Expression.Block(new[] { enumeratorVar, indexVar, lengthVar },
                enumeratorAssign,
                indexInit,
                lengthInit,
                Expression.Loop(
                    Expression.IfThenElse(
                        Expression.Equal(moveNextCall, Expression.Constant(true)),
                        Expression.Block(new[] { loopVar },
                            Expression.Assign(loopVar, Expression.Property(enumeratorVar, "Current")),
                            body,
                            indexIncrement
                        ),
                        Expression.Break(breakLabel)
                    ),
                breakLabel));

            return loop;
        }

        private static IRuntime CreateRuntime(IAny model)
        {
            return AppContext.Current.Container.GetInstance<IRuntimeBuilder>()
                .Create(DataActions.Read, null, SecurityContext.Current.ToUser(), model, model?.ModelType ?? typeof(IAny), new Core.Auditing.AuditedChange[0]);
        }


        private Type ParseModelType(string query)
        {
            IEnumerable<BQL.BQLError> errors;
            if (BQL.Validate(query, out errors))
            {
                var bql = BQL.Parse(query);
                var pipeline = new JoinQueryPipeline<IAny>(bql);
                var root = pipeline.First();
                var returns = pipeline.Last() as JoinReturnQueryStep<IAny>;
                Type returnType = null;
                if (pipeline.OfType<JoinAggregateQueryStep<IAny>>().Count() == 1)
                {
                    // get the model type returned
                    var predicate = (root as JoinAggregateQueryStep<IAny>).Predicate as BQLParser.PredicateExpressionContext;
                    var vertex = predicate.GetChild(0) as BQLParser.VertexAccessorContext;
                    var entity = vertex.GetChild(0) as BQLParser.QualifiedElementContext;
                    var entityName = entity.GetText();
                    returnType = ModelTypeManager.GetModelType(entityName);
                    if (returns.ReturnType == Core.Grammar.ReturnType.Paths)
                    {
                        returnType = typeof(Path<>).MakeGenericType(returnType);
                    }
                }
                else if (pipeline.OfType<JoinAggregateQueryStep<IAny>>().Count() > 1 && returns.ReturnType == Core.Grammar.ReturnType.Nodes)
                {
                    returnType = typeof(IAny);
                }
                return returnType;
            }
            else
            {
                throw new InvalidOperationException(string.Format("Invalid BQL Query: '{0}'", query));
            }
        }

        private class Any : IAny
        {
            public Any() { }

            public DateTime Created
            {
                get;

                set;
            }

            public bool IsDeleted
            {
                get;

                set;
            }

            public bool IsNew
            {
                get
                {
                    return true;
                }
            }

            public Type ModelType
            {
                get
                {
                    return typeof(IAny);
                }
            }

            public DateTime Modified
            {
                get;

                set;
            }

            public event PropertyChangedEventHandler PropertyChanged;
            public event PropertyChangingEventHandler PropertyChanging;

            public IEnumerable<AuditedChange> Compare(IModel model, string prefix)
            {
                return new AuditedChange[0];
            }

            public string GetKey()
            {
                return "0";
            }

            public void SetKey(string value)
            {

            }
        }

        Stack<Expression> _currentLambda = new Stack<Expression>();
        Expression _rootLambda = null;
        Expression _runtime = null;
        Expression _replaced = null;

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (node.Equals(_replaced))
            {
                return Expression.Property(_runtime, "Model");
            }
            else
            {
                return node;
            }
        }

        protected override Expression VisitLambda(LambdaExpression node)
        {
            _currentLambda.Push(node);
            try
            {
                if (_currentLambda.Count == 1)
                {
                    _rootLambda = node;
                }


                ParameterExpression[] parms;
                if (_currentLambda.Peek() == _rootLambda)
                {
                    parms = new ParameterExpression[] { _runtime as ParameterExpression };
                    _replaced = ((LambdaExpression)node).Parameters[0];
                }
                else
                {
                    parms = ((LambdaExpression)node).Parameters.Select(p => Visit(p)).OfType<ParameterExpression>().ToArray();
                }

                var body = Visit(((LambdaExpression)node).Body);
                return Expression.Lambda(body, parms);
            }
            finally
            {
                _currentLambda.Pop();
            }
        }
    }
}
