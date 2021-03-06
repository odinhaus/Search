﻿using Data.Core.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core
{
    public class ModelSet<T> : ModelSet, IModelSet<T> where T : IModel
    {
        public ModelSet(QueryProvider provider, IRepository repository, Expression expression)
            :base(provider, repository, typeof(T), expression)
        {
            this.Repository = repository;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return ((IEnumerable<T>)this._provider.Execute(this.Expression)).GetEnumerator();
        }

        public T Get(T model)
        {
            return (T)Get((IModel)model);
        }
    }

    public class ModelSet : IModelSet
    {
        public ModelSet(QueryProvider provider, IRepository repository, Type identityType, Expression expression)
        {
            _provider = provider;
            if (expression == null)
            {
                this.Expression = Expression.Constant(this);
            }
            else
            {
                this.Expression = expression;
            }
            this.Repository = repository;
            this.ElementType = identityType;
        }

        public Expression Expression
        {
            get;
            private set;
        }

        protected IPersistableProvider _provider;
        IPersistableProvider IPersistable.Provider
        {
            get { return _provider; }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)this._provider.Execute(this.Expression)).GetEnumerator();
        }

        public Type ElementType { get; private set; }

        public IRepository Repository
        {
            get;
            protected set;
        }

        public virtual IModel Get(IModel identity)
        {
            return this.Repository.Get(identity);
        }
    }
}
