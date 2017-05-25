using Common.Diagnostics;
using Data.Core;
using Data.Core.Compilation;
using Data.Core.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Data.Core.Linq;
using Common;
using Common.Security;
using Data.Core.Security;
using Newtonsoft.Json;
using Common.Serialization;
using System.Runtime.Caching;
using Data.Core.Web;
using Data.Core.Auditing;

namespace Data.ArangoDB
{
    public class ModelPersistenceProviderBase<T> : IModelPersistenceProvider<T> where T : IModel
    {
        public virtual int Delete(DeleteExpression expression)
        {
            return Delete((T)expression.Model);
        }

        public virtual int Delete(T item)
        {
            var db = DbContext.Create();
            var aql = QueryBuilder.BuildDeleteQuery<T>(item);

            item = Get(item);

            if (item == null)
            {
                return 0;
            }

#if (DEBUG)
            Logger.LogInfo(aql);
#endif

            string failureMessage;
            if (DataAccessSecurityContext.Current.IsEnabled && !DataAccessSecurityContext.Current.Demand(SecurityContext.Current.ToUser(), item, DataActions.Delete, out failureMessage, null, null))
            {
                throw new System.Security.SecurityException(failureMessage);
            }

            var deleted = db.Query.Aql(aql).ToDocuments().Value.First();

            var auditer = AppContext.Current.Container.GetInstance<IAuditer>();
            auditer.Audit(SecurityContext.Current?.CurrentPrincipal?.Identity ?? null, new IModel[] { (IModel)item }, AuditEventType.Delete);

            return 1;
        }

        [Override(typeof(GetOverride))]
        public virtual T Get(T item)
        {
            var db = DbContext.Create();
            var aql = QueryBuilder.BuildGetQuery<T>(item);
#if (DEBUG)
            Logger.LogInfo(aql);
#endif
            var model = db.Query.Aql(aql).ToDocuments().Value?.FirstOrDefault();
            if (model == null)
            {
                return default(T);
            }
            var savedItem = RuntimeModelBuilder.CreateModelInstanceActivator<T>(ModelTypeConverter.ModelBaseType)(model);
            var auditer = AppContext.Current.Container.GetInstance<IAuditer>();
            auditer.Audit(SecurityContext.Current?.CurrentPrincipal?.Identity ?? null, new IModel[] { (IModel)savedItem }, AuditEventType.Read);

            string failureMessage;
            if (DataAccessSecurityContext.Current.IsEnabled && !DataAccessSecurityContext.Current.Demand(SecurityContext.Current.ToUser(), savedItem, DataActions.Read, out failureMessage, null, null))
            {
                throw new System.Security.SecurityException(failureMessage);
            }
            if (savedItem is ILink)
            {
                var link = savedItem as LinkBase;
                var _to = link._to.Replace("_", ".").Split('/');
                var _from = link._from.Replace("_", ".").Split('/');
                var toType = ModelTypeManager.GetModelType(link.TargetType);
                var toQuery = QueryBuilder.BuildGetQuery(toType, _to[1]);
                var fromType = ModelTypeManager.GetModelType(link.SourceType);
                var fromQuery = QueryBuilder.BuildGetQuery(fromType, _from[1]);
                var to = db.Query.Aql(toQuery).ToDocument().Value;
                link.To = (IModel)RuntimeModelBuilder.CreateModelInstanceActivator(toType)(to);
                var from = db.Query.Aql(fromQuery).ToDocument().Value;
                link.From = (IModel)RuntimeModelBuilder.CreateModelInstanceActivator(fromType)(from);
                if (DataAccessSecurityContext.Current.IsEnabled && !DataAccessSecurityContext.Current.Demand(SecurityContext.Current.ToUser(), link.From, DataActions.Read, out failureMessage, null, null))
                {
                    throw new System.Security.SecurityException(failureMessage);
                }
                if (DataAccessSecurityContext.Current.IsEnabled && !DataAccessSecurityContext.Current.Demand(SecurityContext.Current.ToUser(), link.To, DataActions.Read, out failureMessage, null, null))
                {
                    throw new System.Security.SecurityException(failureMessage);
                }
            }
            return savedItem;
        }

        public virtual T Save(SaveExpression expression)
        {
            if (expression.Model.IsNew)
            {
                return Create((T)expression.Model, expression.Owner);
            }
            else
            {
                return Update((T)expression.Model);
            }
        }

        public virtual T Create(T item, IOrgUnit owner)
        {
            if (!item.IsNew && Get(item) != null)
            {
                throw new InvalidOperationException(string.Format("A model of type {0} with Key of {1} already exists.  Use a unique key, or Update the existing item.", 
                    ModelTypeManager.GetModelName<T>(), item.GetKey()));
            }
            var isInitialSetup = false;
            if (owner == null || owner.IsNew)
            {
                if (item is IOrgUnit &&
                    ((IOrgUnit)item).Name == IOrgUnitDefaults.RootOrgUnitName &&
                    SecurityContext.Current?.CurrentPrincipal?.Identity?.Name == IUserDefaults.AdministrativeUser)
                {
                    isInitialSetup = true;
                }
                else
                {
                    throw new InvalidOperationException("An existing, valid owner is required to create a new item.");
                }
            }


            var qp = AppContext.Current.Container.GetInstance<IModelQueryProviderBuilder>().CreateQueryProvider<IAny>();
            if (item is ILink)
            {
                if (((ILink)item).From.IsNew)
                {
                    ((ILink)item).From = Di.CreateModel(((ILink)item).From, owner);
                }
                else
                {
                    ((ILink)item).From = qp.Query(string.Format("{0}{{Key={1}}}", ModelTypeManager.GetModelName(((ILink)item).From.ModelType), ((ILink)item).From.GetKey())).FirstOrDefault() as IModel;
                }
                if (((ILink)item).To.IsNew)
                {
                    ((ILink)item).To = Di.CreateModel(((ILink)item).To, owner);
                }
                else
                {
                    ((ILink)item).To = qp.Query(string.Format("{0}{{Key={1}}}", ModelTypeManager.GetModelName(((ILink)item).To.ModelType), ((ILink)item).To.GetKey())).FirstOrDefault() as IModel;
                }
            }

            if (!isInitialSetup)
                owner = qp.Query(string.Format("{0}{{Key={1}}}", ModelTypeManager.GetModelName(owner.ModelType), owner.Key)).FirstOrDefault() as IOrgUnit;

            string failureMessage;
            if (item is _IOwnedModel)
            {
                ((_IOwnedModel)item).Owner = owner;
            }

            if (item is owns)
            {
                if (DataAccessSecurityContext.Current.IsEnabled &&
                    (!DataAccessSecurityContext.Current.Demand(SecurityContext.Current.ToUser(), item, DataActions.Link, out failureMessage, null, null)))
                {
                    throw new System.Security.SecurityException(failureMessage);
                }
            }
            else
            {
                var ownsTest = Model.New<owns>();
                ownsTest.From = owner;
                ownsTest.To = item;

                if (DataAccessSecurityContext.Current.IsEnabled &&
                    (!DataAccessSecurityContext.Current.Demand(SecurityContext.Current.ToUser(), item, item is ILink ? DataActions.Link : DataActions.Create, out failureMessage, null, null)
                    || !DataAccessSecurityContext.Current.Demand(SecurityContext.Current.ToUser(), ownsTest, DataActions.Link, out failureMessage, null, null)
                    ))
                {
                    throw new System.Security.SecurityException(failureMessage);
                }
            }
            if (item is _IOwnedModel)
            {
                ((_IOwnedModel)item).Owner = null;
            }

            var db = DbContext.Create();

            var isNew = item.IsNew;
            item.Created = item.Modified = DateTime.Now;

            T previous = default(T);
            if (!isNew)
            {
                lock (_lockedModels)
                {
                    previous = (T)_lockedModels[item.GlobalKey()];
                }

                if (previous == null)
                {
                    previous = Get(item);
                }
            }

            var aql = QueryBuilder.BuildCreateQuery<T>(item);

#if (DEBUG)
            Logger.LogInfo(aql);
#endif

            var reads = item is ILink
                    ? new string[] { ModelCollectionManager.GetCollectionName<IUser>(), ModelCollectionManager.EdgeCollection }
                    : new string[] { ModelCollectionManager.GetCollectionName<T>(), ModelCollectionManager.GetCollectionName<IUser>(), ModelCollectionManager.EdgeCollection };
            var writes = item is ILink
                ? new string[] { ModelCollectionManager.EdgeCollection }
                : new string[] { ModelCollectionManager.GetCollectionName<T>() };

            var result = db.AqlTransacted(aql, writes, reads);
            var doc = result?.FirstOrDefault();

            if (doc == null || doc.Count == 0)
                throw new InvalidOperationException("The record could not be found, or the user does not hold a current lock at the time of saving.");

            var model = RuntimeModelBuilder.CreateModelInstanceActivator<T>(ModelTypeConverter.ModelBaseType)(doc);
            if (item is ILink)
            {
                ((ILink)model).To = ((ILink)item).To;
                ((ILink)model).From = ((ILink)item).From;
            }

            if (!typeof(T).Implements<owns>() && !isInitialSetup)
            {
                var ompp = AppContext.Current.Container.GetInstance<IModelPersistenceProviderBuilder>().CreatePersistenceProvider<owns>();
                var owns = Model.New<owns>();

                owns.From = owner;
                owns.To = model;

                ompp.Create(owns, owner);
            }

            if (model is _IOwnedModel)
            {
                ((_IOwnedModel)model).Owner = owner;
            }

            Lock(model);

            var auditer = AppContext.Current.Container.GetInstance<IAuditer>();
            auditer.Audit(
                SecurityContext.Current?.CurrentPrincipal?.Identity ?? null,
                new IModel[]
                {
                    (IModel)model
                },
                AuditEventType.Create,
                model.ToJson());

            return model;
        }

        public T Update(T item)
        {
            if (item.IsNew) throw new InvalidCastException("The item needs to be created before being updated.");

            var db = DbContext.Create();

            var isNew = item.IsNew;
            item.Modified = DateTime.Now;

            var qp = AppContext.Current.Container.GetInstance<IModelQueryProviderBuilder>().CreateQueryProvider<IAny>();
            var owner = qp.Query(string.Format(
                                    "{0}->{1}->{2}{{Key = {3}}}", 
                                    ModelTypeManager.GetModelName<IOrgUnit>(),
                                    ModelTypeManager.GetModelName<owns>(),
                                    ModelTypeManager.GetModelName<T>(),
                                    item.GetKey())).FirstOrDefault() as IOrgUnit;
            ((_IOwnedModel)item).Owner = owner;

            string failureMessage;
            if (DataAccessSecurityContext.Current.IsEnabled && !DataAccessSecurityContext.Current.Demand(SecurityContext.Current.ToUser(), item, DataActions.Update, out failureMessage, null, null))
            {
                throw new System.Security.SecurityException(failureMessage);
            }

            T previous = default(T);
            if (!isNew)
            {
                lock (_lockedModels)
                {
                    previous = (T)_lockedModels[item.GlobalKey()];
                }

                if (previous == null)
                {
                    previous = Get(item);
                }
            }

            var aql = QueryBuilder.BuildUpdateQuery<T>(item);

#if (DEBUG)
            Logger.LogInfo(aql);
#endif

            var reads = item is ILink
                    ? new string[] { ModelCollectionManager.GetCollectionName<IUser>(), ModelCollectionManager.EdgeCollection }
                    : new string[] { ModelCollectionManager.GetCollectionName<T>(), ModelCollectionManager.GetCollectionName<IUser>(), ModelCollectionManager.EdgeCollection };
            var writes = item is ILink
                ? new string[] { ModelCollectionManager.EdgeCollection }
                : new string[] { ModelCollectionManager.GetCollectionName<T>() };

            var result = db.AqlTransacted(aql, writes, reads);
            var doc = result?.FirstOrDefault();

            if (doc == null || doc.Count == 0)
                throw new InvalidOperationException("The record could not be found, or the user does not hold a current lock at the time of saving.");

            var model = RuntimeModelBuilder.CreateModelInstanceActivator<T>(ModelTypeConverter.ModelBaseType)(doc);
            if (item is ILink)
            {
                ((ILink)model).To = ((ILink)item).To;
                ((ILink)model).From = ((ILink)item).From;
            }

            Lock(model);

            var auditer = AppContext.Current.Container.GetInstance<IAuditer>();
            auditer.Audit(
                SecurityContext.Current?.CurrentPrincipal?.Identity ?? null,
                new IModel[]
                {
                    (IModel)model
                },
                AuditEventType.Update,
                model.Compare(previous, "").ToArray().ToJson());

            return model;
        }

        static MemoryCache _lockedModels = new MemoryCache("LockedModels");
        public LockedModel<T> Lock(T item)
        {
            var audit = AuditSettings.IsEnabled;
            var sec = DataAccessSecurityContext.Current.IsEnabled;
            try
            {
                AuditSettings.IsEnabled = false;
                DataAccessSecurityContext.Current.IsEnabled = false;

                var db = DbContext.Create();
                var aql = QueryBuilder.BuildLockQuery<T>(item);

#if (DEBUG)
                Logger.LogInfo(aql);
#endif
                try
                {
                    var reads = item is ILink
                        ? new string[] { ModelCollectionManager.GetCollectionName<IUser>(), ModelCollectionManager.EdgeCollection }
                        : new string[] { ModelCollectionManager.GetCollectionName<T>(), ModelCollectionManager.GetCollectionName<IUser>(), ModelCollectionManager.EdgeCollection };
                    var writes = new string[] { ModelCollectionManager.EdgeCollection };

                    var result = db.AqlTransacted(aql, writes, reads);
                    var doc = result?.First();
                    if (doc == null || result == null || result.Count == 0 || doc.Count == 0) return null;
                    {
                        var lm = new LockedModel<T>();
                        lm.Lock = RuntimeModelBuilder.CreateModelInstanceActivator<ILock>(ModelTypeConverter.ModelBaseType)(doc["Lock"] as Dictionary<String, object>);
                        lm.Model = RuntimeModelBuilder.CreateModelInstanceActivator<T>(ModelTypeConverter.ModelBaseType)(doc["Model"] as Dictionary<String, object>);
                        lock (_lockedModels)
                        {
                            var key = lm.Model.GlobalKey();
                            if (key != null)
                            {
                                _lockedModels.Remove(key);
                                _lockedModels.Add(key, lm.Model, new CacheItemPolicy() { AbsoluteExpiration = lm.Lock.Expires.AddMinutes(2) });
                            }
                        }
                        return lm;
                    }
                }
                catch
                {
                    return null;
                }
            }
            finally
            {
                AuditSettings.IsEnabled = audit;
                DataAccessSecurityContext.Current.IsEnabled = sec;
            }
        }


        public ILock Unlock(T item)
        {
            var audit = AuditSettings.IsEnabled;
            var sec = DataAccessSecurityContext.Current.IsEnabled;
            try
            {
                AuditSettings.IsEnabled = false;
                DataAccessSecurityContext.Current.IsEnabled = false;
                var db = DbContext.Create();
                var aql = QueryBuilder.BuildUnlockQuery(item);
#if (DEBUG)
                Logger.LogInfo(aql);
#endif

                var result = db.AqlTransacted(aql,
                    new string[] { ModelCollectionManager.EdgeCollection },
                    new string[] { ModelCollectionManager.GetCollectionName<IUser>(), ModelCollectionManager.EdgeCollection });
                lock (_lockedModels)
                {
                    var key = item.GlobalKey();
                    _lockedModels.Remove(key);
                }
                var doc = result?.FirstOrDefault() ?? null;
                if (doc != null)
                    return RuntimeModelBuilder.CreateModelInstanceActivator<ILock>(ModelTypeConverter.ModelBaseType)(doc);
                else
                    return null;
            }
            finally
            {
                AuditSettings.IsEnabled = audit;
                DataAccessSecurityContext.Current.IsEnabled = sec;
            }
        }
    }
}
