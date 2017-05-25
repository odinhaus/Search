using Microsoft.CSharp.RuntimeBinder;
using Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Linq
{
    /// <summary>
    /// Converts LINQ query operators to into custom DbExpression's
    /// </summary>
    public class QueryBinder : ExpressionVisitor
    {
        QueryMapper mapper;
        QueryLanguage language;
        Dictionary<ParameterExpression, Expression> map;
        Expression root;
        IModelSet batchUpd;
        Type repoType;

        protected QueryBinder(QueryMapper mapper, Expression root)
        {
            this.mapper = mapper;
            this.language = mapper.Translator.Linguist.Language;
            this.map = new Dictionary<ParameterExpression, Expression>();
            this.root = root;
            this.repoType = mapper.Mapping.RepositoryType;
        }

        protected QueryLanguage Language { get { return language; } }

        protected Dictionary<ParameterExpression, Expression> Map { get { return map; } }

        protected QueryMapper Mapper { get { return mapper; } }

        protected Type RepositoryType { get { return repoType; } }

        public static Expression Bind(QueryMapper mapper, Expression expression)
        {
            return new QueryBinder(mapper, expression).Visit(expression);
        }

        protected static LambdaExpression GetLambda(Expression e)
        {
            while (e.NodeType == ExpressionType.Quote)
            {
                e = ((UnaryExpression)e).Operand;
            }
            if (e.NodeType == ExpressionType.Constant)
            {
                return ((ConstantExpression)e).Value as LambdaExpression;
            }
            return e as LambdaExpression;
        }

        public ModelAlias GetNextAlias()
        {
            return new ModelAlias();
        }

        protected override Expression VisitMethodCall(MethodCallExpression m)
        {
            if (m.Method.DeclaringType.Implements(typeof(IRepository)))
            {
                IModelSet upd = this.batchUpd != null
                    ? this.batchUpd
                    : GetSetFromExpression(m.Arguments[0]);

                switch (m.Method.Name)
                {
                    case "Insert":
                        return this.BindSave(
                            upd,
                            m.Arguments[1],
                            m.Arguments[2]);
                    case "Update":
                        return this.BindSave(
                            upd,
                            m.Arguments[1],
                            Expression.Constant(null, typeof(IOrgUnit)));
                    case "Delete":
                        if (m.Arguments.Count == 2 && GetLambda(m.Arguments[1]) != null)
                        {
                            return this.BindDelete(upd, null);
                        }
                        return this.BindDelete(
                            upd,
                            m.Arguments[1]);
                }
            }
            else if (m.Method.DeclaringType == typeof(Persistable))
            {
                switch (m.Method.Name)
                {
                    case "Traverse":
                        {
                            if (m.Arguments.Count == 3)
                            {
                                var lambda = (LambdaExpression)((UnaryExpression)m.Arguments[1]).Operand;
                                var root = lambda.Compile().DynamicInvoke();
                                var source = Expression.Convert(Expression.Constant(root), lambda.Type.GetGenericArguments()[0]);
                                return this.BindTraverse(m.Type, source, GetLambda(m.Arguments[2]));
                            }
                            break;
                        }
                    case "ReturnPrivate":
                        {
                            if (m.Arguments.Count == 2)
                            {
                                var traverse = (TraverseExpression)this.Visit(m.Arguments[0]);
                                var returns = (TraverseReturnsExpression)((LambdaExpression)((UnaryExpression)this.Visit(m.Arguments[1])).Operand).Body;
                                return new TraverseExpression(traverse.Type, traverse.Origin, traverse.PathFilter, returns);
                            }
                            break;
                        }
                }
            }
            else if (m.Method.DeclaringType.IsGenericType && m.Method.DeclaringType.GetGenericTypeDefinition() == typeof(PathSelector<>))
            {
                switch (m.Method.Name)
                {
                    case "Out":
                        {
                            var parent = base.Visit(m.Object);
                            if (parent is ParameterExpression)
                            {
                                parent = new PathRootFilterExpression(parent.Type.GetGenericArguments()[0]);
                            }
                            var exp = this.BindOutFilter(m.Type, GetLambda(m.Arguments[0]), parent);
                            return exp;
                        }
                    case "In":
                        {
                            var parent = base.Visit(m.Object);
                            if (parent is ParameterExpression)
                            {
                                parent = new PathRootFilterExpression(parent.Type.GetGenericArguments()[0]);
                            }
                            var exp = this.BindInFilter(m.Type, GetLambda(m.Arguments[0]), parent);
                            return exp;
                        }
                }
            }
            else if (m.Method.DeclaringType.IsGenericType && m.Method.DeclaringType.GetGenericTypeDefinition() == typeof(PathReturner<>))
            {
                var nodeDepth = 0;
                var edgeDepth = 0;

                var parent = (Expression)m;
                var returns = ReturnsType.Model;
                Type returnsType = null;
                do
                {
                    
                    if (parent is ParameterExpression)
                    {
                        nodeDepth++;
                    }
                    else if (parent is MethodCallExpression)
                    {
                        if (((MethodCallExpression)parent).Method.Name.Equals("Return"))
                        {
                            returnsType = ((MethodCallExpression)parent).Method.ReturnType;
                            if (((MethodCallExpression)parent).Method.ReturnType.Implements<ILink>())
                            {
                                returns = ReturnsType.Edge;
                            }
                        }
                        else
                        { 
                            if (((MethodCallExpression)parent).Method.Name.Equals("Edge"))
                            {
                                edgeDepth++;
                            }
                            else
                            {
                                nodeDepth++;
                            }
                        }
                        parent = ((MethodCallExpression)parent).Object;
                    }
                } while (!(parent is ParameterExpression));
                return new TraverseReturnsExpression(returnsType, returns == ReturnsType.Edge ? edgeDepth : nodeDepth, returns);
            }

            return base.VisitMethodCall(m);
        }

        private Expression BindInFilter(Type type, LambdaExpression lambdaExpression, Expression parent)
        {
            var genTypes = lambdaExpression.Type.GetGenericArguments();
            if (genTypes.Length == 3)
            {
                var edgeType = genTypes[0];
                var nodeType = genTypes[1];
                var filter = new InEdgeNodeFilterExpression(EdgeSelectionType.Inclusive, edgeType, nodeType, base.Visit(lambdaExpression.Body), parent);
                return filter;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private Expression BindOutFilter(Type type, LambdaExpression lambdaExpression, Expression parent)
        {
            var genTypes = lambdaExpression.Type.GetGenericArguments();
            if (genTypes.Length == 3)
            {
                var edgeType = genTypes[0];
                var nodeType = genTypes[1];
                var filter = new OutEdgeNodeFilterExpression(EdgeSelectionType.Inclusive, edgeType, nodeType, base.Visit(lambdaExpression.Body), parent);
                return filter;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        bool _isTraversing = false;
        protected virtual Expression BindTraverse(Type type, Expression source, LambdaExpression selector)
        {
            _isTraversing = true;
            var root = source is ConstantExpression ? (ConstantExpression)source : (ConstantExpression)TypedSubtreeFinder.Find(source, type.GetGenericArguments()[0].GetGenericArguments()[0]);
            var pathType = typeof(Path<>).MakeGenericType(root.Type);
            var predicate = base.Visit(selector);
            if (predicate is LambdaExpression)
            {
                predicate = ((LambdaExpression)predicate).Body;
                if (predicate is UnaryExpression)
                {
                    predicate = ((UnaryExpression)predicate).Operand;
                }
            }
            var origin = new TraverseOriginExpression(source.Type, ((IModel)root.Value).GetKey());
            var traverse = new TraverseExpression(type, origin, (EdgeNodeFilterExpression)predicate, null);
            _isTraversing = false;
            return traverse;
        }

        protected virtual Expression BindReturn(Type type, Expression source, LambdaExpression selector)
        {
            return source;
        }

        private IModelSet GetSetFromExpression(Expression expression)
        {
            if (expression is ConstantExpression)
            {
                return ((ConstantExpression)expression).Value as IModelSet;
            }
            else if (expression is UnaryExpression)
            {
                return ((ConstantExpression)((UnaryExpression)expression).Operand).Value as IModelSet;
            }
            else throw new NotSupportedException("Cannot retrieve set from the given expression.");
        }

        protected override Expression VisitUnary(UnaryExpression u)
        {
            if ((u.NodeType == ExpressionType.Convert || u.NodeType == ExpressionType.ConvertChecked)
                && u == this.root)
            {
                this.root = u.Operand;
            }
            return base.VisitUnary(u);
        }

        
        private NewExpression GetNewExpression(Expression expression)
        {
            // ignore converions 
            while (expression.NodeType == ExpressionType.Convert || expression.NodeType == ExpressionType.ConvertChecked)
            {
                expression = ((UnaryExpression)expression).Operand;
            }
            return expression as NewExpression;
        }

        private Expression BindSave(IModelSet upd, Expression instance, Expression owner)
        {
            return new SaveExpression((IModel)((ConstantExpression)instance).Value, (IOrgUnit)((ConstantExpression)owner).Value);
        }

        private Expression BindDelete(IModelSet upd, Expression instance)
        {
            return new DeleteExpression((IModel)((ConstantExpression)instance).Value);
        }

        private bool IsQuery(Expression expression)
        {
            Type elementType = TypeHelper.GetElementType(expression.Type);
            return elementType != null && typeof(IPersistable<>).MakeGenericType(elementType).IsAssignableFrom(expression.Type);
        }

        protected override Expression VisitConstant(ConstantExpression c)
        {
            if (this.IsQuery(c))
            {
                return new PredicateExpression(c);
            }
            return c;
        }

        protected override Expression VisitParameter(ParameterExpression p)
        {
            Expression e;
            if (this.map.TryGetValue(p, out e))
            {
                return e;
            }
            return p;
        }

        protected override Expression VisitBinary(BinaryExpression b)
        {
            switch (b.NodeType)
            {
                case ExpressionType.AndAlso:
                    {
                        return new AndExpression(base.Visit(ConvertMemberExpressionToEquals(b.Left)), base.Visit(ConvertMemberExpressionToEquals(b.Right)));
                    }
                case ExpressionType.OrElse:
                    {
                        return new OrExpression(base.Visit(ConvertMemberExpressionToEquals(b.Left)), base.Visit(ConvertMemberExpressionToEquals(b.Right)));
                    }
                case ExpressionType.Equal:
                    {
                        var exp = new EQExpression(GetCompositeMemberAccessName(b.Left.NodeType == ExpressionType.MemberAccess ? (MemberExpression)b.Left : (MemberExpression)b.Right),
                           MakeScalarExpression(b.Left, b.Right));
                        return BindPathMemberAccess(b, exp);
                    }
                case ExpressionType.NotEqual:
                    {
                        var exp = new NEQExpression(GetCompositeMemberAccessName(b.Left.NodeType == ExpressionType.MemberAccess ? (MemberExpression)b.Left : (MemberExpression)b.Right),
                            MakeScalarExpression(b.Left, b.Right));
                        return BindPathMemberAccess(b, exp);
                    }
                case ExpressionType.GreaterThan:
                    {
                        var exp = new GTExpression(GetCompositeMemberAccessName(b.Left.NodeType == ExpressionType.MemberAccess ? (MemberExpression)b.Left : (MemberExpression)b.Right),
                            MakeScalarExpression(b.Left, b.Right));
                        return BindPathMemberAccess(b, exp);
                    }
                case ExpressionType.GreaterThanOrEqual:
                    {
                        var exp = new GTEExpression(GetCompositeMemberAccessName(b.Left.NodeType == ExpressionType.MemberAccess ? (MemberExpression)b.Left : (MemberExpression)b.Right),
                            MakeScalarExpression(b.Left, b.Right));
                        return BindPathMemberAccess(b, exp);
                    }
                case ExpressionType.LessThan:
                    {
                        var exp = new LTExpression(GetCompositeMemberAccessName(b.Left.NodeType == ExpressionType.MemberAccess ? (MemberExpression)b.Left : (MemberExpression)b.Right),
                            MakeScalarExpression(b.Left, b.Right));
                        return BindPathMemberAccess(b, exp);
                    }
                case ExpressionType.LessThanOrEqual:
                    {
                        var exp = new LTEExpression(GetCompositeMemberAccessName(b.Left.NodeType == ExpressionType.MemberAccess ? (MemberExpression)b.Left : (MemberExpression)b.Right),
                            MakeScalarExpression(b.Left, b.Right));
                        return BindPathMemberAccess(b, exp);
                    }
                default:
                    {
                        return base.VisitBinary(b);
                    }
            }
        }

        private BinarySerializableExpression MakeScalarExpression(Expression left, Expression right)
        {
            if (left.NodeType == ExpressionType.MemberAccess)
            {
                var value = ((ConstantExpression)right).Value;
                var type = ((MemberExpression)left).Type;
                return new ScalarExpression(value, type);
            }
            else
            {
                var value = ((ConstantExpression)left).Value;
                var type = ((MemberExpression)right).Type;
                return new ScalarExpression(value, type);
            }
        }

        protected virtual Expression ConvertMemberExpressionToEquals(Expression exp)
        {
            if (exp is MemberExpression && ((MemberExpression)exp).Type.Equals(typeof(bool)))
            {
                return Expression.Equal(exp, Expression.Constant(true));
            }
            return exp;
        }

        protected virtual Expression BindPathMemberAccess(BinaryExpression b, Expression expression)
        {
            if (_isTraversing)
            {
                var type = GetBinaryMemberType(b);
                if (type.Implements<ILink>())
                {
                    return new PathEdgeFilterMemberAccessExpression(type, expression);
                }
                else
                {
                    return new PathNodeFilterMemberAccessExpression(type, expression);
                }
            }
            return expression;
        }

        private Type GetBinaryMemberType(BinaryExpression b)
        {
            var member = b.Left.NodeType == ExpressionType.MemberAccess ? (MemberExpression)b.Left : (MemberExpression)b.Right;
            return member.Expression.Type;
        }

        protected virtual string GetCompositeMemberAccessName(MemberExpression expression)
        {
            string name = "";
            do
            {
                if (name.Length > 0)
                {
                    name = "." + name;
                }

                name = expression.Member.Name + name;
                expression = expression.Expression as MemberExpression;
            } while (expression != null && expression.NodeType == ExpressionType.MemberAccess);
            return name;
        }

        protected override Expression VisitInvocation(InvocationExpression iv)
        {
            LambdaExpression lambda = iv.Expression as LambdaExpression;
            if (lambda != null)
            {
                for (int i = 0, n = lambda.Parameters.Count; i < n; i++)
                {
                    this.map[lambda.Parameters[i]] = iv.Arguments[i];
                }
                return this.Visit(lambda.Body);
            }
            return base.VisitInvocation(iv);
        }

        public static Expression BindMember(Expression source, MemberInfo member)
        {
            switch (source.NodeType)
            {
                //case (ExpressionType)QueryExpressionType.Entity:
                //    EntityExpression ex = (EntityExpression)source;
                //    var result = BindMember(ex.Expression, member);
                //    MemberExpression mex = result as MemberExpression;
                //    if (mex != null && mex.Expression == ex.Expression && mex.Member == member)
                //    {
                //        return Expression.MakeMemberAccess(source, member);
                //    }
                //    return result;

                case ExpressionType.Convert:
                    UnaryExpression ux = (UnaryExpression)source;
                    return BindMember(ux.Operand, member);

                case ExpressionType.MemberInit:
                    MemberInitExpression min = (MemberInitExpression)source;
                    for (int i = 0, n = min.Bindings.Count; i < n; i++)
                    {
                        MemberAssignment assign = min.Bindings[i] as MemberAssignment;
                        if (assign != null && MembersMatch(assign.Member, member))
                        {
                            return assign.Expression;
                        }
                    }
                    break;

                case ExpressionType.New:
                    NewExpression nex = (NewExpression)source;
                    if (nex.Members != null)
                    {
                        for (int i = 0, n = nex.Members.Count; i < n; i++)
                        {
                            if (MembersMatch(nex.Members[i], member))
                            {
                                return nex.Arguments[i];
                            }
                        }
                    }
                    break;

                case ExpressionType.Conditional:
                    ConditionalExpression cex = (ConditionalExpression)source;
                    return Expression.Condition(cex.Test, BindMember(cex.IfTrue, member), BindMember(cex.IfFalse, member));

                case ExpressionType.Constant:
                    ConstantExpression con = (ConstantExpression)source;
                    Type memberType = TypeHelper.GetMemberType(member);
                    if (con.Value == null)
                    {
                        return Expression.Constant(GetDefault(memberType), memberType);
                    }
                    else
                    {
                        return Expression.Constant(GetValue(con.Value, member), memberType);
                    }
            }

            if (source.Type.Implements<IModel>())
            {
                var binder = Microsoft.CSharp.RuntimeBinder.Binder.GetMember(
                    Microsoft.CSharp.RuntimeBinder.CSharpBinderFlags.None,
                    member.Name,
                    source.Type,
                    new[] { CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null) });
                return Expression.Dynamic(binder, TypeHelper.GetMemberType(member), source);
            }
            else
            {
                return Expression.MakeMemberAccess(source, member);
            }
        }

        private static object GetValue(object instance, MemberInfo member)
        {
            FieldInfo fi = member as FieldInfo;
            if (fi != null)
            {
                return fi.GetValue(instance);
            }
            PropertyInfo pi = member as PropertyInfo;
            if (pi != null)
            {
                return pi.GetValue(instance, null);
            }
            return null;
        }

        private static object GetDefault(Type type)
        {
            if (!type.IsValueType || TypeHelper.IsNullableType(type))
            {
                return null;
            }
            else
            {
                return Activator.CreateInstance(type);
            }
        }

        private static bool MembersMatch(MemberInfo a, MemberInfo b)
        {
            if (a.Name == b.Name)
            {
                return true;
            }
            if (a is MethodInfo && b is PropertyInfo)
            {
                return a.Name == ((PropertyInfo)b).GetGetMethod().Name;
            }
            else if (a is PropertyInfo && b is MethodInfo)
            {
                return ((PropertyInfo)a).GetGetMethod().Name == b.Name;
            }
            return false;
        }
    }
}
