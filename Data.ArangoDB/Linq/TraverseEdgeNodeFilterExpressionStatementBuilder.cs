using Data.Core;
using Data.Core.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Data.ArangoDB.Linq
{
    public class TraverseEdgeNodeFilterExpressionStatementBuilder : Data.Core.Linq.ExpressionVisitor
    {
        StringBuilder _sb;
        private string _itemName;

        public static string Create<T>(EdgeNodeFilterExpression expression)
        {
            var sb = new StringBuilder();
            var visitor = new TraverseEdgeNodeFilterExpressionStatementBuilder(sb);
            visitor.Visit(expression);
            return sb.ToString();
        }

        private TraverseEdgeNodeFilterExpressionStatementBuilder(StringBuilder sb)
        {
            _sb = sb;
            _sb.Append("FILTER ");
        }

        int _depth = 0;
        Type _currentEdgeType, _currentNodeType;
        protected override Expression Visit(Expression exp)
        {
            if (exp == null)
                return null;

            var nodeType = (QueryExpressionType)exp.NodeType;
            switch (nodeType)
            {
                case QueryExpressionType.TraverseOrigin:
                case QueryExpressionType.PathRootFilter:
                    {
                        return exp;
                    }
                case QueryExpressionType.PathNodeFilterMember:
                    {
                        _itemName = string.Format("p.vertices[{0}]", _depth + 1); // nodes are always one level deeper, due to the root node at position 0
                        this.Visit(((PathNodeFilterMemberAccessExpression)exp).Predicate);
                        return exp;
                    }
                case QueryExpressionType.PathEdgeFilterMember:
                    {
                        _itemName = string.Format("p.edges[{0}]", _depth);
                        this.Visit(((PathEdgeFilterMemberAccessExpression)exp).Predicate);
                        return exp;
                    }
                case QueryExpressionType.OutEdgeNodeFilter:
                case QueryExpressionType.InEdgeNodeFilter:
                    {
                        var inExp = (EdgeNodeFilterExpression)exp;

                        
                        this.Visit(inExp.Parent);
                        

                        _currentEdgeType = ModelTypeManager.GetModelType(inExp.EdgeType);
                        _currentNodeType = ModelTypeManager.GetModelType(inExp.ModelType);
                        
                        var isIn = inExp is InEdgeNodeFilterExpression;
                        string sourceType, targetType;

                        if (isIn)
                        {
                            targetType = GetModelTypeFromParent(inExp.Parent);
                            sourceType = inExp.ModelType;
                        }
                        else
                        {
                            sourceType = GetModelTypeFromParent(inExp.Parent);
                            targetType = inExp.ModelType;
                        }

                        if (_currentEdgeType == typeof(any))
                        {
                            _sb.Append(string.Format("{3}(p.edges[{0}].TargetType == \"{1}\" && p.edges[{0}].SourceType == \"{2}\"{4}",
                                _depth,
                                targetType,
                                sourceType,
                                _depth > 0 ? " && " : "",
                                inExp.Predicate != null ? " && " : ""));
                        }
                        else
                        {
                            _sb.Append(string.Format("{4}(p.edges[{0}].ModelType == \"{1}\" && p.edges[{0}].TargetType == \"{2}\" && p.edges[{0}].SourceType == \"{3}\"{5}",
                                _depth,
                                inExp.EdgeType,
                                targetType,
                                sourceType,
                                _depth > 0 ? " && " : "",
                                inExp.Predicate != null ? " && " : ""));
                        }
                        this.Visit(inExp.Predicate);
                        _sb.Append(")");

                        _depth++;
                        return exp;
                    }
                case QueryExpressionType.EQ:
                    {
                        var eqExp = (EQExpression)exp;
                        _sb.Append(string.Format("{2}.{0} == {1}", MemberName(eqExp.MemberName), JsonValue(eqExp.Value, false), _itemName));
                        return exp;
                    }
                case QueryExpressionType.NE:
                    {
                        var eqExp = (NEQExpression)exp;
                        _sb.Append(string.Format("{2}.{0} != {1}", MemberName(eqExp.MemberName), JsonValue(eqExp.Value), _itemName));
                        return exp;
                    }
                case QueryExpressionType.GT:
                    {
                        var eqExp = (GTExpression)exp;
                        _sb.Append(string.Format("{2}.{0} > {1}", MemberName(eqExp.MemberName), JsonValue(eqExp.Value), _itemName));
                        return exp;
                    }
                case QueryExpressionType.GTE:
                    {
                        var eqExp = (GTEExpression)exp;
                        _sb.Append(string.Format("{2}.{0} >= {1}", MemberName(eqExp.MemberName), JsonValue(eqExp.Value), _itemName));
                        return exp;
                    }
                case QueryExpressionType.LT:
                    {
                        var eqExp = (LTExpression)exp;
                        _sb.Append(string.Format("{2}.{0} < {1}", MemberName(eqExp.MemberName), JsonValue(eqExp.Value), _itemName));
                        return exp;
                    }
                case QueryExpressionType.LTE:
                    {
                        var eqExp = (LTEExpression)exp;
                        _sb.Append(string.Format("{2}.{0} <= {1}", MemberName(eqExp.MemberName), JsonValue(eqExp.Value), _itemName));
                        return exp;
                    }
                case QueryExpressionType.Contains:
                    {
                        var eqExp = (StartsWithExpression)exp;

                        _sb.Append(string.Format("LOWER({1}.{0}) LIKE CONCAT(\"%\",",
                            MemberName(eqExp.MemberName),
                            _itemName));

                        Visit(eqExp.Value);

                        _sb.Append(",\"%\")");

                        return exp;
                    }
                case QueryExpressionType.StartsWith:
                    {
                        var eqExp = (StartsWithExpression)exp;

                        _sb.Append(string.Format("LOWER({1}.{0}) LIKE CONCAT(",
                            MemberName(eqExp.MemberName),
                            _itemName));

                        Visit(eqExp.Value);

                        _sb.Append(",\"%\")");

                        return exp;
                    }
                case QueryExpressionType.And:
                    {
                        var andExp = (AndExpression)exp;
                        _sb.Append(" (");
                        this.Visit(andExp.Left);
                        _sb.Append(" && ");
                        this.Visit(andExp.Right);
                        _sb.Append(") ");
                        return exp;
                    }
                case QueryExpressionType.Or:
                    {
                        var andExp = (OrExpression)exp;
                        _sb.Append(" (");
                        this.Visit(andExp.Left);
                        _sb.Append(" || ");
                        this.Visit(andExp.Right);
                        _sb.Append(") ");
                        return exp;
                    }
                case QueryExpressionType.Date_Add:
                case QueryExpressionType.Date_Day:
                case QueryExpressionType.Date_DayOfWeek:
                case QueryExpressionType.Date_DayOfYear:
                case QueryExpressionType.Date_Diff:
                case QueryExpressionType.Date_Hour:
                case QueryExpressionType.Date_ISO8601:
                case QueryExpressionType.Date_Millisecond:
                case QueryExpressionType.Date_Minute:
                case QueryExpressionType.Date_Month:
                case QueryExpressionType.Date_Second:
                case QueryExpressionType.Date_Subtract:
                case QueryExpressionType.Date_Timestamp:
                case QueryExpressionType.Date_Year:
                    {
                        var func = (FunctionExpression)exp;
                        _sb.Append(nodeType.ToString().ToUpper() + "(");
                        for (int i = 0; i < func.Args.Length; i++)
                        {
                            if (i > 0)
                                _sb.Append(",");
                            Visit(func.Args[i]);
                        }
                        _sb.Append(")");
                        return exp;
                    }
                case QueryExpressionType.Scalar:
                    {
                        _sb.Append(((ScalarExpression)exp).Value.ToString());
                        return exp;
                    }
            }


            return base.Visit(exp);
        }

        private string GetModelTypeFromParent(Expression parent)
        {
            if (parent is EdgeNodeFilterExpression)
            {
                return ((EdgeNodeFilterExpression)parent).ModelType;
            }
            else
            {
                return ((ModelAttribute)((PathRootFilterExpression)parent).ModelType.GetCustomAttributes(typeof(ModelAttribute), true)[0]).FullyQualifiedName;
            }
        }

        private string MemberName(string memberName)
        {
            if (memberName.Equals("Key"))
            {
                return "_key";
            }
            else
            {
                return memberName;
            }
        }

        private object JsonValue(object value, bool toLower = true)
        {
            if (value is string || value is DateTime)
            {
                return string.Format("\"{0}\"", toLower ? value.ToString().ToLower() : value.ToString());
            }
            else
            {
                return value?.ToString() ?? "null";
            }
        }
    }
}
