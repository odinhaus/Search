using Common;
using Common.Security;
using Data.Core;
using Data.Core.Security;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.ArangoDB
{
    public class DataContextInitializer : IDataContextInitializer
    {
        public void Initialize()
        {
            var models = ModelTypeManager.ModelTypes.ToArray();
            var db = DbContext.Create(DbContext.ROOT);

            if (!(db.GetAllDatabases().Value?.Any(d => d.Equals(Common.AppContext.Name)) ?? false))
            {
                var result = db.Create(Common.AppContext.Name);
            }

            db = DbContext.Create();

            var collections = db.GetAllCollections();
            var edgeCreated = false;

            foreach (var model in models)
            {
                if (model.Implements<ISubModel>())
                    continue;

                var modelName = ModelCollectionManager.GetCollectionName(model);

                if (!collections.Value.Any(c => c.ContainsKey("name") && c["name"].ToString().Equals(modelName)))
                {
                    if (model.Implements<ILink>())
                    {
                        if (!edgeCreated || model.Implements<IAuditEvent>())
                        {
                            db.Collection.Type(Arango.Client.ACollectionType.Edge).Create(modelName);
                            edgeCreated = true;
                        }
                    }
                    else
                    {
                        db.Collection.Type(Arango.Client.ACollectionType.Document).Create(modelName);
                    }
                }

                var members = ModelTypeManager.GetModelMembers(model).Where(m => m.IsSearchable || m.IsUnique).ToArray();

                if (members.Length > 0)
                {
                    var indices = db.Collection.GetAllIndexes(modelName);
                    if (indices.Success)
                    {
                        var indexes = indices.Value.First(kvp => kvp.Key.Equals("indexes")).Value as IList<object>;
                        var fields = "";
                        foreach (var member in members)
                        {
                            if (fields.Length > 0)
                                fields += ",";
                            fields += member.Name;
                            if (!indexes.Any(o => ((List<object>)((Dictionary<string, object>)o)["fields"]).Any(oo => oo.ToString().Equals(member.Name))))
                            {
                                db.Index.Type(Arango.Client.AIndexType.Hash).Unique(member.IsUnique).Fields(member.Name).Create(modelName);
                            }
                        }
                        if (!string.IsNullOrEmpty(fields))
                        {
                            db.Index.Type(Arango.Client.AIndexType.Hash).Fields(fields.Split(',')).Create(modelName);
                        }
                    }
                }
            }


            var mqp = AppContext.Current.Container.GetInstance<IModelQueryProviderBuilder>().CreateQueryProvider<IUser>();

            var cp = SecurityContext.Current?.CurrentPrincipal;
            try
            {
                SecurityContext.Current.CurrentPrincipal = IUserDefaults.Administrator;
                SecurityContext.Current.ScopeId = Guid.NewGuid().ToString();

                var oui = AppContext.Current.Container.GetInstance<IOrgUnitInitializer>();
                IOrgUnitDefaults.RootOrgUnit = oui.Create(IOrgUnitDefaults.RootOrgUnitName, IOrgUnitDefaults.RootOrgUnitName);

                var results = mqp.Query(string.Format("{0}{{Username='{1}'}}", ModelTypeManager.GetModelName(typeof(IUser)), IUserDefaults.UnauthorizedUser));
                if (results == null || results.Count == 0)
                {
                    var mpp = AppContext.Current.Container.GetInstance<IModelPersistenceProviderBuilder>().CreatePersistenceProvider<IUser>();
                    var user = Model.New<IUser>();
                    user.Username = IUserDefaults.UnauthorizedUser;
                    user.Title = IUserDefaults.UnauthorizedUserTitle;
                    user.Icon = Convert.FromBase64String(IUserDefaults.DefaultIcon);
                    user.IsSystemUser = true;
                    mpp.Create(user, IOrgUnitDefaults.RootOrgUnit);
                }

                results = mqp.Query(string.Format("{0}{{Name='{1}'}}", ModelTypeManager.GetModelName(typeof(IRole)), IRoleDefaults.AdministratorsRoleName));
                if (results == null || results.Count == 0)
                {
                    var mpp = AppContext.Current.Container.GetInstance<IModelPersistenceProviderBuilder>().CreatePersistenceProvider<IRole>();
                    var role = Model.New<IRole>();
                    role.Name = IRoleDefaults.AdministratorsRoleName;
                    IRoleDefaults.AdministratorsRole = mpp.Create(role, IOrgUnitDefaults.RootOrgUnit);
                }
                else
                {
                    IRoleDefaults.AdministratorsRole = results.OfType<IRole>().First();
                }

                results = mqp.Query(string.Format("{0}{{Name='{1}'}}", ModelTypeManager.GetModelName(typeof(IRole)), IRoleDefaults.UsersRoleName));
                if (results == null || results.Count == 0)
                {
                    var mpp = AppContext.Current.Container.GetInstance<IModelPersistenceProviderBuilder>().CreatePersistenceProvider<IRole>();
                    var role = Model.New<IRole>();
                    role.Name = IRoleDefaults.UsersRoleName;
                    IRoleDefaults.UsersRole = mpp.Create(role, IOrgUnitDefaults.RootOrgUnit);
                }
                else
                {
                    IRoleDefaults.UsersRole = results.OfType<IRole>().First();
                }

                results = mqp.Query(string.Format("{0}{{Username='{1}'}}", ModelTypeManager.GetModelName(typeof(IUser)), IUserDefaults.AdministrativeUser));
                if (results == null || results.Count == 0)
                {
                    var mpp = AppContext.Current.Container.GetInstance<IModelPersistenceProviderBuilder>().CreatePersistenceProvider<IUser>();
                    var user = Model.New<IUser>();
                    user.Username = IUserDefaults.AdministrativeUser;
                    user.Title = IUserDefaults.AdministrativeUserTitle;
                    user.Icon = Convert.FromBase64String(IUserDefaults.DefaultIcon);
                    user.IsSystemUser = true;
                    user = mpp.Create(user, IOrgUnitDefaults.RootOrgUnit);
                    var memberAdmin = Model.New<isMemberOf>();
                    memberAdmin.From = user;
                    memberAdmin.To = IRoleDefaults.AdministratorsRole;
                    var mppmo = AppContext.Current.Container.GetInstance<IModelPersistenceProviderBuilder>().CreatePersistenceProvider<isMemberOf>();
                    mppmo.Create(memberAdmin, IOrgUnitDefaults.RootOrgUnit);
                    var memberUser = Model.New<isMemberOf>();
                    memberUser.From = user;
                    memberUser.To = IRoleDefaults.AdministratorsRole;
                    mppmo.Create(memberUser, IOrgUnitDefaults.RootOrgUnit);
                }
            }
            finally
            {
                SecurityContext.Current.CurrentPrincipal = cp;
            }
        }
    }
}
