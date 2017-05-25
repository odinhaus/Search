using Common;
using Data.Core.Evaluation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Data.Core.Templating.Grammar;
using System.Reflection;

namespace Data.Core.Scripting
{
    public class EditableCodeContext : CodeContext
    {
        static Dictionary<string, EditableCodeContext> _contexts = new Dictionary<string, EditableCodeContext>();

        public new static EditableCodeContext Create(string name, Type runtimeType, Type modelType, string checksum)
        {
            if (!runtimeType.Implements<IEditableRuntime>())
                throw new InvalidOperationException("The Runtime type must implement IEditableRuntime");

            ResetStack();

            EditableCodeContext context = null;
            lock (_contexts)
            {
                if (name == null || !_contexts.TryGetValue(name + modelType.FullName, out context) || !context.Checksum.Equals(checksum) || !context.IsCompleted)
                {
                    if (context != null)
                    {
                        _contexts.Remove(name + modelType.FullName);
                    }
                    context = new EditableCodeContext(name, runtimeType, modelType, checksum);

                    if (name != null)
                        _contexts.Add(name + modelType.FullName, context);
                }
            }
            return context;
        }

        public EditableCodeContext(string name, Type runtimeType, Type modelType, string checksum) 
            : base(name, runtimeType, modelType, checksum)
        {
        }

        protected override void Initialize(string name, Type runtimeType, Type modelType, string checksum)
        {
            if (!modelType.Implements<IModel>())
                throw new InvalidOperationException("The modelType must implement the IModel interface.");

            VariableScope.Clear();

            this.Checksum = checksum;
            this.Name = name;
            this.RuntimeType = runtimeType;
            this.ModelType = modelType;
            this.Statements = new List<Expression>();

            this.AppendMethod = typeof(StringBuilder).GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
                                              .Single(mi => mi.Name.Equals("Append") && mi.GetParameters().Length == 1 && mi.GetParameters().All(p => p.ParameterType.Equals(typeof(string))));

            Expression iruntime, runtime = null;
            if (!TryGetParameter("@iruntime", out iruntime))
            {
                iruntime = Expression.Parameter(typeof(IEditableRuntime), "@iruntime");
                runtime = Expression.Variable(RuntimeType, "@runtime");
                VariableScope.Current.Add("@iruntime", iruntime);
                VariableScope.Current.Add("@runtime", runtime);
                Statements.Add(Expression.Assign(runtime, Convert(iruntime, RuntimeType)));
            }
            else
            {
                TryGetParameter("@runtime", out runtime);
            }
            this.IRuntime = (ParameterExpression)iruntime;
            this.Runtime = (ParameterExpression)runtime;


            if (!VariableScope.Current.IsRoot)
                VariableScope.Push();
        }

        protected override bool TryVisitSpecialFunction(string functionName, DDVParser.FunctionContext ctx, out Expression special)
        {
            switch(functionName)
            {
                default:
                    {
                        return base.TryVisitSpecialFunction(functionName, ctx, out special);
                    }
                case "Create":
                    {
                        special = VisitCreateFunction(ctx);
                        return true;
                    }
                case "Update":
                    {
                        special = VisitUpdateFunction(ctx);
                        return true;
                    }
                case "Get":
                    {
                        special = VisitGetFunction(ctx);
                        return true;
                    }
                case "Delete":
                    {
                        special = VisitDeleteFunction(ctx);
                        return true;
                    }
            }
        }

        private Expression VisitDeleteFunction(DDVParser.FunctionContext ctx)
        {
            var model = Visit(ctx.GetChild(2));

            if (model.Type.Implements<IModel>())
            {
                return Expression.Call(this.Runtime, this.Runtime.Type.GetMethod("Delete").MakeGenericMethod(ModelTypeManager.GetModelType(model.Type)), model);
            }
            else
            {
                // we need to do a late-bound invocation of the generic Update<T>(T model) call, because we don't have the 
                // the model types available to us at compile time - the invoker will resolve this at runtime to execute the call
                var invokeMethod = typeof(Invoker).GetMethod("InvokeMethod", BindingFlags.Public | BindingFlags.Static);
                // public static object InvokeMethod(object source, string name, params object[] args)
                var argExps = new List<Expression>();
                Expression target = null; // static call
                argExps.Add(target);
                argExps.Add(Expression.Constant("Delete"));
                argExps.Add(Expression.NewArrayInit(typeof(object), new Expression[] { model }));
                return Expression.Call(target, invokeMethod, argExps.ToArray());
            }
        }

        private Expression VisitGetFunction(DDVParser.FunctionContext ctx)
        {
            var model = Visit(ctx.GetChild(2));

            if (model.Type.Implements<IModel>())
            {
                return Expression.Call(this.Runtime, this.Runtime.Type.GetMethod("Get").MakeGenericMethod(ModelTypeManager.GetModelType(model.Type)), model);
            }
            else
            {
                // we need to do a late-bound invocation of the generic Update<T>(T model) call, because we don't have the 
                // the model types available to us at compile time - the invoker will resolve this at runtime to execute the call
                var invokeMethod = typeof(Invoker).GetMethod("InvokeMethod", BindingFlags.Public | BindingFlags.Static);
                // public static object InvokeMethod(object source, string name, params object[] args)
                var argExps = new List<Expression>();
                Expression target = null; // static call
                argExps.Add(target);
                argExps.Add(Expression.Constant("Get"));
                argExps.Add(Expression.NewArrayInit(typeof(object), new Expression[] { model }));
                return Expression.Call(target, invokeMethod, argExps.ToArray());
            }
        }

        private Expression VisitUpdateFunction(DDVParser.FunctionContext ctx)
        {
            var model = Visit(ctx.GetChild(2));

            if (model.Type.Implements<IModel>())
            {
                return Expression.Call(this.Runtime, this.Runtime.Type.GetMethod("Update").MakeGenericMethod(ModelTypeManager.GetModelType(model.Type)), model);
            }
            else
            {
                // we need to do a late-bound invocation of the generic Update<T>(T model) call, because we don't have the 
                // the model types available to us at compile time - the invoker will resolve this at runtime to execute the call
                var invokeMethod = typeof(Invoker).GetMethod("InvokeMethod", BindingFlags.Public | BindingFlags.Static);
                // public static object InvokeMethod(object source, string name, params object[] args)
                var argExps = new List<Expression>();
                Expression target = null; // static call
                argExps.Add(target);
                argExps.Add(Expression.Constant("Update"));
                argExps.Add(Expression.NewArrayInit(typeof(object), new Expression[] { model }));
                return Expression.Call(target, invokeMethod, argExps.ToArray());
            }
        }

        private Expression VisitCreateFunction(DDVParser.FunctionContext ctx)
        {
            var model = Visit(ctx.GetChild(2));
            var orgUnit = Visit(ctx.GetChild(4));

            if (model.Type.Implements<IModel>() && orgUnit.Type.Implements<IOrgUnit>())
            {
                return Expression.Call(this.Runtime, this.Runtime.Type.GetMethod("Create").MakeGenericMethod(ModelTypeManager.GetModelType(model.Type)), model, orgUnit);
            }
            else
            {
                // we need to do a late-bound invocation of the generic Create<T>(T model, IOrgUnit orgUnit) call, because we don't have the 
                // the model types available to us at compile time - the invoker will resolve this at runtime to execute the call
                var invokeMethod = typeof(Invoker).GetMethod("InvokeMethod", BindingFlags.Public | BindingFlags.Static);
                // public static object InvokeMethod(object source, string name, params object[] args)
                var argExps = new List<Expression>();
                Expression target = null; // static call
                argExps.Add(target);
                argExps.Add(Expression.Constant("Create"));
                argExps.Add(Expression.NewArrayInit(typeof(object), new Expression[] { model, orgUnit }));
                return Expression.Call(target, invokeMethod, argExps.ToArray());
            }
        }
    }
}
