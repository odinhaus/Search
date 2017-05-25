using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Linq
{
    public class ListFilterVisitor : ExpressionVisitor
    {
        public static Expression Convert(Expression predicate)
        {
            var visitor = new ListFilterVisitor();
            return visitor.Visit(PartialEvaluator.Eval(predicate));
        }

        protected override Expression Visit(Expression exp)
        {
            return base.Visit(exp);
        }

        protected override Expression VisitUnary(UnaryExpression exp)
        {
            return this.Visit(exp.Operand); // strip out the unary expressions - we dont care about them
        }

        protected override Expression VisitBinary(BinaryExpression b)
        {
            var left = this.Visit(b.Left);
            var right = this.Visit(b.Right);
            if (!right.Type.Equals(left.Type))
            {
                var converter = left.NodeType == ExpressionType.MemberAccess
                    ? System.ComponentModel.TypeDescriptor.GetConverter(left.Type)
                    : System.ComponentModel.TypeDescriptor.GetConverter(right.Type);
                if (converter.CanConvertFrom(left.NodeType == ExpressionType.MemberAccess ? right.Type : left.Type))
                {
                    var value = converter.ConvertFrom(left.NodeType == ExpressionType.MemberAccess ? ((ConstantExpression)right).Value : ((ConstantExpression)left).Value);
                    if (left.NodeType == ExpressionType.MemberAccess)
                    {
                        right = Expression.Constant(value);
                    }
                    else
                    {
                        left = Expression.Constant(value);
                    }
                }
            }
            switch (b.NodeType)
            {
                case ExpressionType.AndAlso:
                    {
                        return new AndExpression(base.Visit(b.Left), base.Visit(b.Right));
                    }
                case ExpressionType.OrElse:
                    {
                        return new OrExpression(base.Visit(b.Left), base.Visit(b.Right));
                    }
                case ExpressionType.Equal:
                    {
                        return new EQExpression(GetCompositeMemberAccessName(left.NodeType == ExpressionType.MemberAccess ? (MemberExpression)left : (MemberExpression)right),
                            MakeScalarExpression(left, right));
                    }
                case ExpressionType.NotEqual:
                    {
                        return new NEQExpression(GetCompositeMemberAccessName(b.Left.NodeType == ExpressionType.MemberAccess ? (MemberExpression)b.Left : (MemberExpression)b.Right),
                            MakeScalarExpression(left, right));
                    }
                case ExpressionType.GreaterThan:
                    {
                        return new GTExpression(GetCompositeMemberAccessName(b.Left.NodeType == ExpressionType.MemberAccess ? (MemberExpression)b.Left : (MemberExpression)b.Right),
                            MakeScalarExpression(left, right));
                    }
                case ExpressionType.GreaterThanOrEqual:
                    {
                        return new GTEExpression(GetCompositeMemberAccessName(b.Left.NodeType == ExpressionType.MemberAccess ? (MemberExpression)b.Left : (MemberExpression)b.Right),
                            MakeScalarExpression(left, right));
                    }
                case ExpressionType.LessThan:
                    {
                        return new LTExpression(GetCompositeMemberAccessName(b.Left.NodeType == ExpressionType.MemberAccess ? (MemberExpression)b.Left : (MemberExpression)b.Right),
                            MakeScalarExpression(left, right));
                    }
                case ExpressionType.LessThanOrEqual:
                    {
                        return new LTEExpression(GetCompositeMemberAccessName(b.Left.NodeType == ExpressionType.MemberAccess ? (MemberExpression)b.Left : (MemberExpression)b.Right),
                            MakeScalarExpression(left, right));
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

        protected override Expression VisitMemberAccess(MemberExpression m)
        {
            return base.VisitMemberAccess(m);
        }

        protected override Expression VisitMethodCall(MethodCallExpression m)
        {
            switch(m.Method.Name)
            {
                case "GetKey":
                    {
                        return this.Visit(Expression.MakeMemberAccess(m.Object, 
                            m.Object.Type.GetInterfaces().SelectMany(i => i.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                                                         .Single(p => p.Name.Equals("Key"))));
                    }
                case "Equals":
                    {
                        var binary = Expression.MakeBinary(ExpressionType.Equal, m.Object, m.Arguments[0]);
                        return this.Visit(binary);
                    }

                case "Contains":
                    {
                        return new ContainsExpression(GetCompositeMemberAccessName(m.Object as MemberExpression), new ScalarExpression(((ConstantExpression)m.Arguments[0]).Value.ToString(), typeof(string)));
                    }
                case "StartsWith":
                    {
                        return new StartsWithExpression(GetCompositeMemberAccessName(m.Object as MemberExpression), new ScalarExpression(((ConstantExpression)m.Arguments[0]).Value.ToString(), typeof(string)));
                    }
                default:
                    {
                        throw new NotSupportedException("This method is not supported");
                    }
            }
        }

        protected override Expression VisitLambda(LambdaExpression lambda)
        {
            var body = base.Visit(lambda.Body);
            return new PredicateExpression(body);
        }
    }
}
