using Antlr4.Runtime.Tree;
using Common;
using Common.Security;
using Data.Core;
using Data.Core.Compilation;
using Data.Core.Grammar;
using Data.Core.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Data.ArangoDB
{
    public class BqlTreeConverter
    {
        static object _sync = new object();
        public static BQLExpression Convert(BQLParser.PredicateExpressionContext predicate)
        {
            return new BqlTreeConverter(predicate).Result;
        }

        protected static IApp App { get; private set; }

        private BqlTreeConverter(BQLParser.PredicateExpressionContext predicate)
        {
            this.Source = predicate;
            //this.Origin = CreateOrigin();
            this.Result = (BQLExpression)this.Visit(this.Source);
        }

//        private TraverseOriginExpression CreateOrigin()
//        {
//            lock(_sync)
//            {
//                if (App == null)
//                {
//#if (DEBUG)
//                    if (AppContext.Current == null)
//                    {
//                        App = RuntimeModelBuilder.CreateModelInstance<IApp>();
//                        App.Key = 12345;

//                    }
//                    else
//                    {
//#endif
//                        var builder = AppContext.Current.Container.GetInstance<IModelListProviderBuilder>();
//                        var provider = builder.CreateListProvider<IApp>();
//                        App = provider.Find(0, 1, a => a.Name == AppContext.Name).Single();
//#if (DEBUG)
//                    }
//#endif
//                }
//            }

//            return new TraverseOriginExpression(typeof(IApp), App.Key.ToString());
//        }

        public BQLExpression Result { get; private set; }
        public BQLParser.PredicateExpressionContext Source { get; private set; }
        public Type RootType { get { return typeof(IApp); } }
        //public TraverseOriginExpression Origin { get; private set; }

        protected virtual Expression Visit(IParseTree context)
        {
            if (context is BQLParser.PredicateExpressionContext)
            {
                /* 
                    predicateExpression
	                :	vertexAccessor ((edgeAccessor | vertexIn | vertexOut | notVertexIn | notVertexOut) vertexAccessor)*
	                ;

                    edgeAccessor
	                :	edgeIn qualifiedElement filterExpression? edgeIn
	                |	edgeOut qualifiedElement filterExpression? edgeOut
	                |	notEdgeIn qualifiedElement filterExpression? notEdgeIn
	                |	notEdgeOut qualifiedElement filterExpression? notEdgeOut
	                ;
                */

                var segments = new List<Expression>();
                Type modelType;
                Type edgeType;
                Expression predicate;
                BQLParser.VertexAccessorContext vertex = null;
                BQLParser.EdgeAccessorContext edge = null;
                BQLParser.VertexInContext vIn = null;
                BQLParser.VertexOutContext vOut = null;
                bool isOut = true;
                EdgeSelectionType selectionType = EdgeSelectionType.Inclusive;

                for(int i = 0; i < context.ChildCount; i++)
                {
                    var exp = context.GetChild(i);
                    if (exp is BQLParser.VertexAccessorContext)
                    {
                        vertex = exp as BQLParser.VertexAccessorContext;
                        ConvertAccessor(edge, vertex, out edgeType, out modelType, out predicate);
                        if (isOut)
                        {
                            segments.Add(new OutEdgeNodeFilterExpression(selectionType, edgeType, modelType, predicate, segments.LastOrDefault()));
                        }
                        else
                        {
                            segments.Add(new InEdgeNodeFilterExpression(selectionType, edgeType, modelType, predicate, segments.LastOrDefault()));
                        }
                    }
                    else if (exp is BQLParser.EdgeAccessorContext)
                    {
                        edge = exp as BQLParser.EdgeAccessorContext;
                        isOut = (edge.GetChild(0) is BQLParser.EdgeOutContext | edge.GetChild(0) is BQLParser.NotEdgeOutContext | edge.GetChild(0) is BQLParser.OptionalEdgeOutContext);
                        selectionType = (edge.GetChild(0) is BQLParser.NotEdgeOutContext | edge.GetChild(0) is BQLParser.NotEdgeInContext) 
                            ? EdgeSelectionType.Exclusive 
                            : (edge.GetChild(0) is BQLParser.OptionalEdgeOutContext | edge.GetChild(0) is BQLParser.OptionalEdgeInContext) 
                                ? EdgeSelectionType.OptionalInclusive 
                                : EdgeSelectionType.Inclusive;
                    }
                    else if (exp is BQLParser.VertexInContext)
                    {
                        vIn = exp as BQLParser.VertexInContext;
                        var name = "<-" + ModelTypeManager.GetModelName(typeof(any)) + "<-";
                        edge = BQL.CreateParser(name).edgeAccessor();
                        selectionType = EdgeSelectionType.Inclusive;
                        isOut = false;
                    }
                    else if (exp is BQLParser.VertexOutContext)
                    {
                        vOut = exp as BQLParser.VertexOutContext;
                        var name = "->" + ModelTypeManager.GetModelName(typeof(any)) + "->";
                        edge = BQL.CreateParser(name).edgeAccessor();
                        selectionType = EdgeSelectionType.Inclusive;
                        isOut = true;
                    }
                    else if (exp is BQLParser.NotVertexInContext)
                    {
                        vIn = exp as BQLParser.VertexInContext;
                        var name = "<~" + ModelTypeManager.GetModelName(typeof(any)) + "<~";
                        edge = BQL.CreateParser(name).edgeAccessor();
                        selectionType = EdgeSelectionType.Exclusive;
                        isOut = false;
                    }
                    else if (exp is BQLParser.NotVertexOutContext)
                    {
                        vOut = exp as BQLParser.VertexOutContext;
                        var name = "~>" + ModelTypeManager.GetModelName(typeof(any)) + "~>";
                        edge = BQL.CreateParser(name).edgeAccessor();
                        selectionType = EdgeSelectionType.Exclusive;
                        isOut = true;
                    }
                    else if (exp is BQLParser.OptionalVertexInContext)
                    {
                        vIn = exp as BQLParser.VertexInContext;
                        var name = "<+" + ModelTypeManager.GetModelName(typeof(any)) + "<+";
                        edge = BQL.CreateParser(name).edgeAccessor();
                        selectionType = EdgeSelectionType.OptionalInclusive;
                        isOut = false;
                    }
                    else if (exp is BQLParser.OptionalVertexOutContext)
                    {
                        vOut = exp as BQLParser.VertexOutContext;
                        var name = "+>" + ModelTypeManager.GetModelName(typeof(any)) + "+>";
                        edge = BQL.CreateParser(name).edgeAccessor();
                        selectionType = EdgeSelectionType.OptionalInclusive;
                        isOut = true;
                    }
                }

                return new BQLExpression(this.RootType, (EdgeNodeFilterExpression)segments.LastOrDefault(), new TraverseReturnsExpression(this.RootType, 0, ReturnsType.Model));
            }
            else if (context is BQLParser.ExpressionContext)
            {
                return Visit(context.GetChild(0));
            }
            else if (context is BQLParser.BinaryExpressionContext)
            {
                var left = context.GetChild(0) as BQLParser.PrimaryBinaryPropertyOperandContext;
                var right = context.GetChild(1) as BQLParser.SecondaryBinaryPropertyOperandContext;
                var property = left.GetChild(0) as BQLParser.PropertyExpressionContext;
                var op = left.GetChild(1) as BQLParser.OperatorContext;

                var value = (BinarySerializableExpression)Visit(right.GetChild(0));

                if (_isEdgeContext)
                {
                    return new PathEdgeFilterMemberAccessExpression(_currentEdgeType, CreateMemberAccess(_currentEdgeType, property, op, value));
                }
                else
                {
                    return new PathNodeFilterMemberAccessExpression(_currentVertexType, CreateMemberAccess(_currentVertexType, property, op, value));
                }
            }
            else if (context is BQLParser.PropertyExpressionContext)
            {
                return new ScalarExpression(GetMemberName(context as BQLParser.PropertyExpressionContext), typeof(string));
            }
            else if (context is BQLParser.SpecialFunctionContext)
            {
                string scalar = "";
                switch (context.GetText())
                {
                    case "@@current_username":
                        {
                            scalar = "\"" + SecurityContext.Current.CurrentPrincipal.Identity.Name + "\"";
                            break;
                        }
                    case "@@current_date":
                        {
                            scalar = "\"" + DateTime.Now.Date + "\"";
                            break;
                        }
                    case "@@current_datetime":
                        {
                            scalar = "\"" + DateTime.Now + "\"";
                            break;
                        }
                }
                return new ScalarExpression(scalar, typeof(string));
            }
            else if (context is BQLParser.BooleanExpressionContext)
            {
                if (context.ChildCount == 1)
                {
                    return Visit(context.GetChild(0));
                }
                else
                {
                    return ConvertBoolean(context.GetChild(0), context.GetChild(1), context.GetChild(2));
                }
            }
            else if (context is BQLParser.DateFunctionContext)
            {
                return Visit(context.GetChild(0));
            }
            else if (context is BQLParser.Date_AddContext)
            {
                /*
                date_Add
	                :	'DATE_ADD(' date ',' integerLiteral ',' date_Unit ')'
	                ;
                */
                var date = Visit(context.GetChild(1)) as BinarySerializableExpression;
                var value = Visit(context.GetChild(3)) as BinarySerializableExpression;
                var units = Visit(context.GetChild(5)) as BinarySerializableExpression;
                return new DateFunctionExpression(DateFunctionType.Date_Add, typeof(DateTime), date, value, units);
            }
            else if (context is BQLParser.Date_DayContext)
            {
                var date = Visit(context.GetChild(1)) as BinarySerializableExpression;
                return new DateFunctionExpression(DateFunctionType.Date_Day, typeof(int), date);
            }
            else if (context is BQLParser.Date_DayOfWeekContext)
            {
                var date = Visit(context.GetChild(1)) as BinarySerializableExpression;
                return new DateFunctionExpression(DateFunctionType.Date_DayOfWeek, typeof(int), date);
            }
            else if (context is BQLParser.Date_DayOfYearContext)
            {
                var date = Visit(context.GetChild(1)) as BinarySerializableExpression;
                return new DateFunctionExpression(DateFunctionType.Date_DayOfYear, typeof(int), date);
            }
            else if (context is BQLParser.Date_DiffContext)
            {
                var date1 = Visit(context.GetChild(1)) as BinarySerializableExpression;
                var date2 = Visit(context.GetChild(3)) as BinarySerializableExpression;
                var units = Visit(context.GetChild(4)) as BinarySerializableExpression;
                return new DateFunctionExpression(DateFunctionType.Date_Diff, typeof(int), date1, date2, units);
            }
            else if (context is BQLParser.Date_HourContext)
            {
                var date = Visit(context.GetChild(1)) as BinarySerializableExpression;
                return new DateFunctionExpression(DateFunctionType.Date_Hour, typeof(int), date);
            }
            else if (context is BQLParser.Date_ISO8601Context)
            {
                var args = new List<BinarySerializableExpression>();
                for (int i = 1; i < context.ChildCount; i+=2)
                {
                    args.Add(Visit(context.GetChild(i)) as BinarySerializableExpression);
                }
                return new DateFunctionExpression(DateFunctionType.Date_ISO8601, typeof(DateTime), args.ToArray());
            }
            else if (context is BQLParser.Date_MillisecondContext)
            {
                var date = Visit(context.GetChild(1)) as BinarySerializableExpression;
                return new DateFunctionExpression(DateFunctionType.Date_Millisecond, typeof(int), date);
            }
            else if (context is BQLParser.Date_MinuteContext)
            {
                var date = Visit(context.GetChild(1)) as BinarySerializableExpression;
                return new DateFunctionExpression(DateFunctionType.Date_Minute, typeof(int), date);
            }
            else if (context is BQLParser.Date_MonthContext)
            {
                var date = Visit(context.GetChild(1)) as BinarySerializableExpression;
                return new DateFunctionExpression(DateFunctionType.Date_Month, typeof(int), date);
            }
            else if (context is BQLParser.Date_SecondContext)
            {
                var date = Visit(context.GetChild(1)) as BinarySerializableExpression;
                return new DateFunctionExpression(DateFunctionType.Date_Second, typeof(int), date);
            }
            else if (context is BQLParser.Date_SubtractContext)
            {
                var date = Visit(context.GetChild(1)) as BinarySerializableExpression;
                var value = Visit(context.GetChild(3)) as BinarySerializableExpression;
                var units = Visit(context.GetChild(5)) as BinarySerializableExpression;
                return new DateFunctionExpression(DateFunctionType.Date_Subtract, typeof(DateTime), date, value, units);
            }
            else if (context is BQLParser.Date_TimestampContext)
            {
                var args = new List<BinarySerializableExpression>();
                for (int i = 0; i < context.ChildCount; i++)
                {
                    args.Add(Visit(context.GetChild(i)) as BinarySerializableExpression);
                }
                return new DateFunctionExpression(DateFunctionType.Date_Timestamp, typeof(DateTime), args.ToArray());
            }
            else if (context is BQLParser.Date_YearContext)
            {
                var date = Visit(context.GetChild(1)) as BinarySerializableExpression;
                return new DateFunctionExpression(DateFunctionType.Date_Year, typeof(int), date);
            }
            else if (context is BQLParser.DateContext)
            {
                /*
                
                date
	                :	stringLiteral
	                |	integerLiteral
	                |	current_Date
	                |	current_DateTime
	                |	date_Timestamp
	                |	date_ISO8601
	                |	date_Add
	                |	date_Subtract
	                ;

                */
                return Visit(context.GetChild(0));
            }
            else if (context is BQLParser.Current_DateContext)
            {
                return new ScalarExpression(DateTime.Now, typeof(DateTime));
            }
            else if (context is BQLParser.Current_DateTimeContext)
            {
                return new ScalarExpression(DateTime.Today, typeof(DateTime));
            }
            else if (context is BQLParser.Date_UnitContext)
            {
                return new ScalarExpression(context.GetText().Replace("'",""), typeof(string));
            }
            else if (context is BQLParser.StringLiteralContext)
            {
                return new ScalarExpression(context.GetText(), typeof(string));
            }
            else if (context is BQLParser.IntegerLiteralContext)
            {
                return new ScalarExpression(context.GetText(), typeof(long));
            }
            else if (context is BQLParser.FunctionContext)
            {
                return Visit(context.GetChild(0));
            }
            else if (context is BQLParser.LiteralContext)
            {
                return Visit(context.GetChild(0));
            }
            else if (context is BQLParser.StringLiteralContext)
            {
                return new ScalarExpression(context.GetText(), typeof(string));
            }
            else if (context is BQLParser.IntegerLiteralContext)
            {
                return new ScalarExpression(context.GetText(), typeof(long));
            }
            else if (context is BQLParser.FloatLiteralContext)
            {
                return new ScalarExpression(context.GetText(), typeof(double));
            }
            else if (context is BQLParser.BooleanLiteralContext)
            {
                return new ScalarExpression(context.GetText(), typeof(bool));
            }

            return null;
        }

        private Expression ConvertBoolean(IParseTree left, IParseTree op, IParseTree right)
        {
            switch(op.GetText())
            {
                case "and":
                case "&":
                    {
                        return new AndExpression(this.Visit(left), this.Visit(right));
                    }
                case "or":
                case "|":
                    {
                        return new OrExpression(this.Visit(left), this.Visit(right));
                    }
                default:
                    throw new NotSupportedException("Boolean operator not supported");
            }
        }

        private Expression CreateMemberAccess(Type source, BQLParser.PropertyExpressionContext property, BQLParser.OperatorContext op, BinarySerializableExpression value)
        {
            var member = GetMemberName(property);
            var memberType = GetMemberType(source, member);

            switch(op.GetText())
            {
                case "=":
                    {
                        return new EQExpression(member, value);
                    }
                case ">":
                    {
                        return new GTExpression(member, value);
                    }
                case ">=":
                    {
                        return new GTEExpression(member, value);
                    }
                case "<":
                    {
                        return new LTExpression(member, value);
                    }
                case "<=":
                    {
                        return new LTEExpression(member, value);
                    }
                case "!=":
                    {
                        return new NEQExpression(member, value);
                    }
                case "contains":
                    {
                        return new ContainsExpression(member, value);
                    }
                case "startswith":
                    {
                        return new StartsWithExpression(member, value);
                    }
                default:
                    throw new NotSupportedException("This operation is not supported");
            }
        }

        private Type GetMemberType(Type source, string member)
        {
            var modelType = source;
            var members = member.Split('.');
            Type type = null;

            for(int i = 0; i < members.Length; i++)
            {
                var property = modelType.GetPublicProperty(members[i]);
                type = property.PropertyType;
                modelType = type;
            }

            return type;
        }

        private string FormatScalar(Type dataType, string scalar)
        {
            //scalar = scalar.Replace("'", "").Replace("\"","");
            if (dataType == typeof(DateTime))
            {
                scalar = "'" + scalar.Replace("'","").Replace("\"","") + "'";
            }
            return scalar;
        }

        private string GetMemberName(BQLParser.PropertyExpressionContext property)
        {
            var name = "";
            for (int i = 0; i < property.ChildCount; i++)
            {
                name += property.GetChild(i).GetText();
            }
            return name;
        }

        bool _isEdgeContext = false;
        Type _currentEdgeType, _currentVertexType;
        protected virtual void ConvertAccessor(BQLParser.EdgeAccessorContext edge, BQLParser.VertexAccessorContext vertex, out Type edgeType, out Type modelType, out Expression predicate)
        {
            /*

            edgeAccessor
	        :	edgeIn qualifiedElement filterExpression? edgeIn
	        |	edgeOut qualifiedElement filterExpression? edgeOut
	        ;

            vertexAccessor
	        :	qualifiedElement filterExpression?
	        ;

            */
            if (edge != null)
            {
                edgeType = GetTypeFromQualifiedName(edge.GetChild(1) as BQLParser.QualifiedElementContext);
            }
            else
            {
                edgeType = null;
            }
            _currentEdgeType = edgeType;

            if (vertex != null)
            {
                modelType = GetTypeFromQualifiedName(vertex.GetChild(0) as BQLParser.QualifiedElementContext);
            }
            else
            {
                modelType = null;
            }
            _currentVertexType = modelType;

            /*

            filterExpression
	        :	'{' expression (options {greedy=false;} : '}')
	        ;

            expression
	        :	binaryExpression
	        |	booleanExpression
	        ;

            booleanExpression
	        :	'(' booleanExpression ')'
	        |	binaryExpression bool binaryExpression
	        |	booleanExpression bool binaryExpression
	        |	binaryExpression bool booleanExpression
	        |	booleanExpression bool booleanExpression
	        ;

            binaryExpression
	        :	primaryBinaryPropertyOperand secondaryBinaryPropertyOperand
	        ;

            primaryBinaryPropertyOperand
	        :	propertyExpression operator
	        ;

            secondaryBinaryPropertyOperand
	        :	literal 
	        |	propertyExpression
	        |	specialFunction
	        ;

            propertyExpression
	        :	(namedElement | literal) ('.' (namedElement | literal) )*
	        ;

            specialFunction
	        :	'@@current_username'
	        |	'@@current_date'
	        |	'@@current_datetime'
	        ;

            operator
	        :	'='
	        |	'>'
	        |	'>='
	        |	'<'
	        |	'<='
	        |	'!='
	        |	'contains'
	        |	'startswith'
	        |	'is'
	        |	'is not'
	        ;

            bool
	        :	'&'
	        |	'|'
	        |	'and'
	        |	'or'
	        ;

            */
            Expression edgePredicate = null;
            if (edge != null && edge.ChildCount == 4)
            {
                _isEdgeContext = true;
                var edgeFilter = edge.GetChild(2) as BQLParser.FilterExpressionContext;
                var expression = edgeFilter.GetChild(1) as BQLParser.ExpressionContext;

                edgePredicate = this.Visit(expression);
            }

            Expression vertexPredicate = null;
            if (vertex != null && vertex.ChildCount == 2)
            {
                _isEdgeContext = false;
                var vertexFilter = vertex.GetChild(1) as BQLParser.FilterExpressionContext;
                var expression = vertexFilter.GetChild(1) as BQLParser.ExpressionContext;

                vertexPredicate = this.Visit(expression);
            }

            predicate = null;
            if (edgePredicate != null && vertexPredicate != null)
            {
                predicate = new AndExpression(edgePredicate, vertexPredicate);
            }
            else if (edgePredicate != null)
            {
                predicate = edgePredicate;
            }
            else if (vertexPredicate != null)
            {
                predicate = vertexPredicate;
            }

        }

        protected Type GetTypeFromQualifiedName(BQLParser.QualifiedElementContext element)
        {
            var name = "";
            foreach(var elem in element.children)
            {
                name = string.Concat(name, elem.GetText());
            }
            return ModelTypeManager.GetModelType(name);
        }
    }
}
