using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Linq
{
    public abstract class QueryLinguist
    {
        QueryLanguage language;
        QueryTranslator translator;

        public QueryLinguist(QueryLanguage language, QueryTranslator translator)
        {
            this.language = language;
            this.translator = translator;
        }

        public QueryLanguage Language
        {
            get { return this.language; }
        }

        public QueryTranslator Translator
        {
            get { return this.translator; }
        }

        /// <summary>
        /// Provides language specific query translation.  Use this to apply language specific rewrites or
        /// to make assertions/validations about the query.
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        public virtual Expression Translate(Expression expression)
        {
            return expression;
        }

        /// <summary>
        /// Converts the query expression into text of this query language
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        public abstract string Format(Expression expression);

        /// <summary>
        /// Determine which sub-expressions must be parameters
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        public virtual Expression Parameterize(Expression expression)
        {
            return Parameterizer.Parameterize(this.language, expression);
        }
    }
}
