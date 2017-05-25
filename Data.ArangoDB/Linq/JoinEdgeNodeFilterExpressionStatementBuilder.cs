using Common;
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
    public class JoinEdgeNodeFilterExpressionStatementBuilder : Data.Core.Linq.ExpressionVisitor
    {
        StringBuilder _sb;
        private string _itemName;

        public static string Create<T>(EdgeNodeFilterExpression expression, bool edgeFilter, int depth)
        {
            var sb = new StringBuilder();
            var visitor = new JoinEdgeNodeFilterExpressionStatementBuilder(sb, edgeFilter, depth);
            visitor.Visit(expression);
            return sb.ToString();
        }

        private JoinEdgeNodeFilterExpressionStatementBuilder(StringBuilder sb, bool edgeFilter, int depth)
        {
            _sb = sb;
            _edgeFilter = edgeFilter;
            _depth = depth;
            _itemName = edgeFilter ? "e" + depth : "v" + depth;
        }

        int _depth = 0;
        private bool _lcase = false;
        private bool _isKeyMember = false;
        private bool _edgeFilter;
        private bool _visitedFilter = false;

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
                        if (!_edgeFilter)
                        {
                            this.Visit(((PathNodeFilterMemberAccessExpression)exp).Predicate);
                            _visitedFilter = true;
                        }
                        return exp;
                    }
                case QueryExpressionType.PathEdgeFilterMember:
                    {
                        if (_edgeFilter)
                        {
                            this.Visit(((PathEdgeFilterMemberAccessExpression)exp).Predicate);
                            _visitedFilter = true;
                        }
                        return exp;
                    }
                case QueryExpressionType.OutEdgeNodeFilter:
                case QueryExpressionType.InEdgeNodeFilter:
                    {
                        var inExp = (EdgeNodeFilterExpression)exp;
                        Visit(inExp.Predicate);
                        return exp;
                    }
                case QueryExpressionType.EQ:
                    {
                        var eqExp = (EQExpression)exp;
                        var memberName = MemberName(eqExp.MemberName);
                        _isKeyMember = memberName.Equals("_key");
                        _sb.Append(string.Format("{1}.{0} == ",
                            memberName,
                            _itemName));

                        Visit(eqExp.Value);
                        _isKeyMember = false;

                        return exp;
                    }
                case QueryExpressionType.NE:
                    {
                        var eqExp = (NEQExpression)exp;

                        var memberName = MemberName(eqExp.MemberName);
                        _isKeyMember = memberName.Equals("_key");
                        _sb.Append(string.Format("{1}.{0} != ",
                            memberName,
                            _itemName));

                        Visit(eqExp.Value);
                        _isKeyMember = false;

                        return exp;
                    }
                case QueryExpressionType.GT:
                    {
                        var eqExp = (GTExpression)exp;

                        var memberName = MemberName(eqExp.MemberName);
                        _isKeyMember = memberName.Equals("_key");
                        _sb.Append(string.Format("{1}.{0} > ",
                            memberName,
                            _itemName));

                        Visit(eqExp.Value);
                        _isKeyMember = false;

                        return exp;
                    }
                case QueryExpressionType.GTE:
                    {
                        var eqExp = (GTEExpression)exp;

                        var memberName = MemberName(eqExp.MemberName);
                        _isKeyMember = memberName.Equals("_key");
                        _sb.Append(string.Format("{1}.{0} >= ",
                            memberName,
                            _itemName));

                        Visit(eqExp.Value);
                        _isKeyMember = false;

                        return exp;
                    }
                case QueryExpressionType.LT:
                    {
                        var eqExp = (LTExpression)exp;

                        var memberName = MemberName(eqExp.MemberName);
                        _isKeyMember = memberName.Equals("_key");
                        _sb.Append(string.Format("{1}.{0} < ",
                            memberName,
                            _itemName));

                        Visit(eqExp.Value);
                        _isKeyMember = false;

                        return exp;
                    }
                case QueryExpressionType.LTE:
                    {
                        var eqExp = (LTEExpression)exp;

                        var memberName = MemberName(eqExp.MemberName);
                        _isKeyMember = memberName.Equals("_key");
                        _sb.Append(string.Format("{1}.{0} <= ",
                            memberName,
                            _itemName));

                        Visit(eqExp.Value);
                        _isKeyMember = false;

                        return exp;
                    }
                case QueryExpressionType.Contains:
                    {
                        var eqExp = (ContainsExpression)exp;
                        _lcase = true;
                        _sb.Append(string.Format("LOWER({1}.{0}) LIKE CONCAT(\"%\",",
                            MemberName(eqExp.MemberName),
                            _itemName));

                        if (eqExp.Value == null)
                        {
                            _sb.Append("\"\"");
                        }
                        else
                        {
                            Visit(eqExp.Value);
                        }

                        _sb.Append(",\"%\")");
                        _lcase = false;
                        return exp;
                    }
                case QueryExpressionType.StartsWith:
                    {
                        var eqExp = (StartsWithExpression)exp;
                        _lcase = true;
                        _sb.Append(string.Format("LOWER({1}.{0}) LIKE CONCAT(",
                            MemberName(eqExp.MemberName),
                            _itemName));

                        if (eqExp.Value == null)
                        {
                            _sb.Append("\"\"");
                        }
                        else
                        {
                            Visit(eqExp.Value);
                        }

                        _sb.Append(",\"%\")");
                        _lcase = false;
                        return exp;
                    }
                case QueryExpressionType.And:
                    {
                        var andExp = (AndExpression)exp;
                        _sb.Append("(");
                        _visitedFilter = false;
                        this.Visit(andExp.Left);
                        var visitedLeft = _visitedFilter;
                        var idx = _sb.Length;
                        _visitedFilter = false;
                        this.Visit(andExp.Right);
                        var visitedRight = _visitedFilter;
                        if (visitedLeft && visitedRight)
                        {
                            _sb.Insert(idx, " && ");
                        }
                        else if (!(visitedLeft && visitedRight))
                        {
                            _sb.Remove(0, 1);
                        }
                        if (visitedLeft || visitedRight)
                        {
                            _sb.Append(")");
                        }
                        //_sb.Append(")");
                        return exp;
                    }
                case QueryExpressionType.Or:
                    {
                        var andExp = (OrExpression)exp;
                        _sb.Append("(");
                        _visitedFilter = false;
                        this.Visit(andExp.Left);
                        var visitedLeft = _visitedFilter;
                        var idx = _sb.Length;
                        _visitedFilter = false;
                        this.Visit(andExp.Right);
                        var visitedRight = _visitedFilter;
                        if (visitedLeft && visitedRight)
                        {
                            _sb.Insert(idx, " || ");
                        }
                        else if (!(visitedLeft && visitedRight))
                        {
                            _sb.Remove(0, 1);
                        }
                        if (visitedLeft || visitedRight)
                        {
                            _sb.Append(")");
                        }
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
                        if (_isKeyMember)
                        {
                            _sb.AppendFormat("\"{0}\"", ((ScalarExpression)exp).Value.ToString());
                        }
                        else
                        {
                            _sb.Append(JsonValue(((ScalarExpression)exp).Value.ToString(), ((ScalarExpression)exp).Type, _lcase));
                        }
                        return exp;
                    }
            }


            return base.Visit(exp);
        }

        private Type GetMemberType(string memberName)
        {
            if (memberName.Equals("_key"))
            {
                return typeof(string);
            }
            else
            {
                return typeof(object);
            }
        }

        private string GetModelTypeFromFilter(EdgeNodeFilterExpression filter)
        {
            var parent = filter.Parent;
            if (parent == null)
            {
                return null;
            }
            else
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
        }

        private string MemberName(string memberName)
        {
            if (memberName.Equals("Key"))
            {
                return "_key";
            }
            else if (memberName.Equals("To"))
            {
                return "_to";
            }
            else if (memberName.Equals("From"))
            {
                return "_from";
            }
            else if (memberName.Equals("Id"))
            {
                return "_id";
            }
            else
            {
                return memberName;
            }
        }

        private object JsonValue(object value, Type type, bool lcaseStrings = true)
        {
            if (type.Equals(typeof(string)))
            {
                var cased = lcaseStrings ? value.ToString().ToLower() : value.ToString();
                if (cased.StartsWith("'"))
                {
                    cased = cased.Substring(1, cased.Length - 1);
                }
                if (cased.StartsWith("\""))
                {
                    cased = cased.Substring(1, cased.Length - 1);
                }
                if (cased.EndsWith("'"))
                {
                    cased = cased.Substring(0, cased.Length - 1);
                }
                if (cased.EndsWith("\""))
                {
                    cased = cased.Substring(0, cased.Length - 1);
                }
                return "\"" + cased + "\"";
            }
            else if (type.Equals(typeof(DateTime)))
            {
                var dt = (DateTime)value;
                if (dt.Kind == DateTimeKind.Unspecified)
                {
                    dt = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, dt.Millisecond, DateTimeKind.Local);
                }
                if (dt.Kind == DateTimeKind.Local)
                {
                    dt = dt.ToUniversalTime();
                }
                return string.Format("'{0}'", ((DateTime)value).ToISO8601());
            }
            else if (type.IsEnum)
            {
                object cast;
                if (value.TryCast(Enum.GetUnderlyingType(value.GetType()), out cast))
                {
                    return cast.ToString();
                }
                else
                {
                    return value.ToString();
                }
            }
            else
            {
                return value?.ToString() ?? "null";
            }
        }
    }
}
