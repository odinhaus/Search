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

namespace Data.Core
{
    public class GetOverride : Data.Core.Linq.ExpressionVisitor, IOverride
    {
        public Argument[] ArgumentTypes
        {
            get
            {
                return new Argument[] { new Argument("projector", typeof(string)) };
            }
        }

        public MethodInfo MethodToCall { get; private set; }
        public Type ModelType { get; private set; }
        public Type ReturnType { get; private set; }

        /// <summary>
        /// Builds a CallExpression that will convert the string projector passed in via argumentsArray element[1] 
        /// to a Func of T, TResult, where T is the model type and TResult is an anonymous type created by the 
        /// projector
        /// </summary>
        /// <param name="provider">the service provider instance parameter</param>
        /// <param name="method">the generic Get method on the provider</param>
        /// <param name="argumentsArray">the arguments in a 2 element array which map to the call</param>
        /// <returns></returns>
        public Expression CreateCall(Expression provider, MethodInfo method, Expression argumentsArray)
        {
            this.ReturnType = typeof(object);
            this.ModelType = method.DeclaringType.GetGenericArguments()[0];
            this.MethodToCall = method;
            var getMethod = typeof(GetOverride).GetMethod("Get", BindingFlags.NonPublic | BindingFlags.Instance).MakeGenericMethod(ModelType);
            var args = new List<Expression>();
            args.Add(provider);
            args.AddRange(MapParameters(argumentsArray, getMethod.GetParameters().Skip(1).ToArray()));
            return Expression.Call(Expression.Constant(this), getMethod, args);
        }

        private IEnumerable<Expression> MapParameters(Expression args, ParameterInfo[] parms)
        {
            for (int p = 0; p < parms.Length; p++)
            {
                // read element p from the passed in IList<object> parameter, args.
                yield return Expression.Convert(Expression.ArrayAccess(args, Expression.Constant(p)), parms[p].ParameterType);
            }
        }

        static Dictionary<string, Delegate> _overrides = new Dictionary<string, Delegate>();
        private object Get<T>(object provider, T model, string projector) where T : IModel
        {
            var checksum = projector.ToBase64MD5();
            Delegate func;
            lock (_overrides)
            {
                if (!_overrides.TryGetValue(checksum, out func))
                {
                    var runtime = AppContext.Current.Container.GetInstance<IRuntimeBuilder>()
                        .Create(Common.Security.DataActions.Read, null, SecurityContext.Current.ToUser(), model, typeof(T), new Core.Auditing.AuditedChange[0]);
                    var codeContext = TemplateContext.Create(checksum, runtime.GetType(), typeof(T), checksum);
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
                            var getMethod = MethodToCall;
                            var parms = new ParameterExpression[]
                            {
                                Expression.Parameter(typeof(T))
                            };
                            var callGet = Expression.Call(Expression.Constant(provider), getMethod, parms); // this will get the model instance from the provider Get(T model)

                            // the projector expression will likely reference an input type of Object as param 1$
                            // we need to rebuild the expression to use the IRuntime environment
                            _runtime = Expression.Parameter(typeof(IRuntime), "@runtime");
                            projectorExp = Visit(projectorExp); // produces Func<IRuntime, TResult>

                            // in order to call the projector, we need to create an instance of IRuntime, assigning the results from Get(T item) to the Model
                            var createRuntime = Expression.Call(null, typeof(GetOverride).GetMethod("CreateRuntime", BindingFlags.NonPublic | BindingFlags.Static), callGet, Expression.Constant(callGet.Type));

                            // now we need to Invoke the Func<IRuntime, TResult> projectorExp, passing in the created IRuntime
                            var invokeProjector = Expression.Invoke(projectorExp, createRuntime);

                            // lastly, compile this as Func, and return the created anonymous type
                            var projectorFinal = Expression.Lambda<Func<T, object>>(Expression.Convert(invokeProjector, typeof(object)), parms);
                            func = projectorFinal.Compile();
                        }
                    }
                    else
                    {
                        func = null;
                    }
                    _overrides.Add(checksum, func);
                }
            }
            if (func == null)
                throw new InvalidOperationException("The projector expression is invalid.");
            else
                return ((Func<T, object>)func)(model);
        }

        private static IRuntime CreateRuntime(IModel model, Type modelType)
        {
            return AppContext.Current.Container.GetInstance<IRuntimeBuilder>()
                .Create(DataActions.Read, null, SecurityContext.Current.ToUser(), model, modelType, new Core.Auditing.AuditedChange[0]);
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
