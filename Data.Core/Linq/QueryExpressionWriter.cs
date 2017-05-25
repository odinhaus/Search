using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Linq
{
    /// <summary>
    /// Writes out an expression tree (including DbExpression nodes) in a C#-ish syntax
    /// </summary>
    public class QueryExpressionWriter : ExpressionWriter
    {
        QueryLanguage language;
        Dictionary<ModelAlias, int> aliasMap = new Dictionary<ModelAlias, int>();

        protected QueryExpressionWriter(TextWriter writer, QueryLanguage language)
            : base(writer)
        {
            this.language = language;
        }

        public new static void Write(TextWriter writer, Expression expression)
        {
            Write(writer, null, expression);
        }

        public static void Write(TextWriter writer, QueryLanguage language, Expression expression)
        {
            new QueryExpressionWriter(writer, language).Visit(expression);
        }

        public new static string WriteToString(Expression expression)
        {
            return WriteToString(null, expression);
        }

        public static string WriteToString(QueryLanguage language, Expression expression)
        {
            StringWriter sw = new StringWriter();
            Write(sw, language, expression);
            return sw.ToString();
        }

        protected override Expression Visit(Expression exp)
        {
            if (exp == null)
                return null;

            switch ((QueryExpressionType)exp.NodeType)
            {
                default:
                    if ((int)exp.NodeType >= (int)QueryExpressionType.MinValue)
                    {
                        this.Write(this.FormatQuery(exp));
                        return exp;
                    }
                    else
                    {
                        return base.Visit(exp);
                    }
            }
        }

        protected void AddAlias(ModelAlias alias)
        {
            if (!this.aliasMap.ContainsKey(alias))
            {
                this.aliasMap.Add(alias, this.aliasMap.Count);
            }
        }

        

        protected virtual string FormatQuery(Expression query)
        {
            return query.ToString();
        }

       

        protected override Expression VisitConstant(ConstantExpression c)
        {
            if (c.Type == typeof(QueryCommand))
            {
                QueryCommand qc = (QueryCommand)c.Value;
                this.Write("new QueryCommand {");
                this.WriteLine(Indentation.Inner);
                this.Write("\"" + qc.CommandText + "\"");
                this.Write(",");
                this.WriteLine(Indentation.Same);
                this.Visit(Expression.Constant(qc.Parameters));
                this.Write(")");
                this.WriteLine(Indentation.Outer);
                return c;
            }
            return base.VisitConstant(c);
        }

        
    }
}
