using Data.Core.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Linq.Expressions;
using Common;
using Data.Core;

namespace Data.ArangoDB.Linq
{
    public class PredicateExpressionStatementBuilder : Data.Core.Linq.ExpressionVisitor
    {
        StringBuilder _sb;
        private string _itemName;
        private string _collectionName;
        private bool _isLink;

        public static string Create<T>(PredicateExpression expression ) where T : IModel
        {
            var sb = new StringBuilder();
            var itemName = QueryBuilder.GetItemName<T>();
            var collectionName = ModelCollectionManager.GetCollectionName<T>();
            var isLink = typeof(T).Implements<ILink>();
            var modelType = ModelTypeManager.GetModelName(typeof(T));

            sb.Append("FILTER " + itemName + ".IsDeleted == false ");
            if (isLink)
            {
                sb.Append(" && " + itemName + ".ModelType == \"" + modelType + "\"");
            }
            var visitor = new PredicateExpressionStatementBuilder(sb, itemName , collectionName, isLink, modelType);
            visitor.Visit(expression);
            return sb.ToString();
        }

        private PredicateExpressionStatementBuilder(StringBuilder sb, string itemName, string collectionName, bool isLink, string modelType)
        {
            _sb = sb;
            _itemName = itemName;
            _collectionName = collectionName;
            _isLink = isLink;
        }

        protected override Expression Visit(Expression exp)
        {
            if (exp == null) return null;

            var nodeType = (QueryExpressionType)exp.NodeType;
            switch(nodeType)
            {
                case QueryExpressionType.Predicate:
                    {
                        _sb.Append(" && ");
                        return this.Visit(((PredicateExpression)exp).Body);
                    }
                case QueryExpressionType.EQ:
                    {
                        var eqExp = (EQExpression)exp;
                        WriteBinaryExpression("==", eqExp);
                        return exp;
                    }
                case QueryExpressionType.NE:
                    {
                        var eqExp = (NEQExpression)exp;
                        WriteBinaryExpression("!=", eqExp);
                        return exp;
                    }
                case QueryExpressionType.GT:
                    {
                        var eqExp = (GTExpression)exp;
                        WriteBinaryExpression(">", eqExp);
                        return exp;
                    }
                case QueryExpressionType.GTE:
                    {
                        var eqExp = (GTEExpression)exp;
                        WriteBinaryExpression(">=", eqExp);
                        return exp;
                    }
                case QueryExpressionType.LT:
                    {
                        var eqExp = (LTExpression)exp;
                        WriteBinaryExpression("<", eqExp);
                        return exp;
                    }
                case QueryExpressionType.LTE:
                    {
                        var eqExp = (LTEExpression)exp;
                        WriteBinaryExpression("<=", eqExp);
                        return exp;
                    }
                case QueryExpressionType.Contains:
                    {
                        var eqExp = (ContainsExpression)exp;

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

                        return exp;
                    }
                case QueryExpressionType.StartsWith:
                    {
                        var eqExp = (StartsWithExpression)exp;

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
                        _sb.Append(JsonValue(((ScalarExpression)exp).Value, ((ScalarExpression)exp).Type));
                        return exp;
                    }
            }


            return base.Visit(exp);
        }

        private void WriteBinaryExpression(string op, IBinaryExpression eqExp)
        {
            var memberName = MemberName(eqExp.MemberName);
            Type type;
            object value = eqExp.Value;
            if (value is ScalarExpression)
            {
                value = ((ScalarExpression)value).Value;
            }
            switch(memberName)
            {
                case "_id":
                case "_to":
                case "_from":
                case "_key":
                    {
                        type = typeof(string);
                        if (eqExp.Value is ScalarExpression)
                        {
                            if (((ScalarExpression)eqExp.Value).Value is IModel)
                            {
                                var model = ((ScalarExpression)eqExp.Value).Value as IModel;
                                if (memberName.Equals("_key"))
                                {
                                    value = model.GetKey();
                                }
                                else
                                {
                                    value = ModelCollectionManager.GetCollectionName(model.ModelType) + "/" + model.GetKey();
                                }
                            }
                            else
                            {
                                value = ((ScalarExpression)eqExp.Value).Value.ToString();
                            }
                        }
                        break;
                    }
                default:
                    {
                        type = value == null ? typeof(object) : value.GetType();
                        break;
                    }
            }

            
            _sb.Append(string.Format("{2}.{0} {3} {1}", memberName, JsonValue(value, type, false), _itemName, op));
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
