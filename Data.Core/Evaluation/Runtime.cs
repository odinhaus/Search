using Microsoft.IdentityModel.Claims;
using Common;
using Common.Diagnostics;
using Common.IO;
using Common.Security;
using Common.Serialization;
using Data.Core.Auditing;
using Data.Core.Linq;
using Data.Core.Security;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.Caching;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Evaluation
{
    public class Runtime : IRuntime
    {
        static MemoryCache _cachedQueries = new MemoryCache("Runtime_Query_Cache");

        public Runtime(DataActions action, string callContextOrgUnit, IUser user, IModel model, Type modelType, IEnumerable<AuditedChange> changes, string customArg = "")
        {
            this.Changes = changes?.ToArray() ?? new AuditedChange[0];
            this.QueryProviderBuilder = AppContext.Current.Container.GetInstance<IModelQueryProviderBuilder>();
            this.Action = action;
            this.Model = model;
            this.RootOrgUnit = IOrgUnitDefaults.RootOrgUnit;
            this.ModelType = modelType;
            this.CustomArg = customArg;
            if (user == null)
            {
                this.User = Data.Model.New<IUser>();
                this.User.Username = IUserDefaults.UnauthorizedUser;
                this.User.Title = IUserDefaults.UnauthorizedUserTitle;
            }
            else
            {
                this.User = user;
            }

            if (model is ILink)
            {
                From = ((ILink)model).From;
                To = ((ILink)model).To;
            }
            this.Now = Altus.Suffūz.CurrentTime.Now;
            this.CallContextOrgUnit = callContextOrgUnit;
        }
        public string CustomArg { get; private set; }
        public IOrgUnit RootOrgUnit { get; private set; }
        public IModel From { get; private set; }
        public IModel Model { get; private set; }
        public DateTime Now { get; private set; }
        public IModelQueryProviderBuilder QueryProviderBuilder { get; private set; }
        public IModel To { get; private set; }
        public IUser User { get; private set; }
        public DataActions Action { get; private set; }
        public string CallContextOrgUnit { get; private set; }
        public string ModelClass { get { return ModelTypeManager.GetModelName(ModelType); } }
        public string ToClass { get { return To == null ? "" : ModelTypeManager.GetModelName(To.ModelType); } }
        public string FromClass { get { return From == null ? "" : ModelTypeManager.GetModelName(From.ModelType); } }

        public virtual IOrgUnit ModelOrgUnit
        {
            get
            {
                if (Model is IOwnedModel && ((IOwnedModel)Model).Owner != null)
                {
                    return ((IOwnedModel)Model).Owner;
                }
                else if (Model is owns)
                {
                    return ((owns)Model).From as IOrgUnit;
                }
                else if (Model.IsNew)
                {
                    return UserOrgUnit;
                }
                else
                {
                    var query = string.Format("{0}->{1}->{2}{{Key = {3}}}",
                                    ModelTypeManager.GetModelName<IOrgUnit>(),
                                    ModelTypeManager.GetModelName<owns>(),
                                    ModelTypeManager.GetModelName(Model.ModelType),
                                    Model.GetKey());
                    return ModelQueryEvaluator<IOrgUnit>(query);
                }
            }
        }

        public virtual IOrgUnit UserOrgUnit
        {
            get
            {
                if (User is IOwnedModel && ((IOwnedModel)User).Owner != null)
                {
                    return ((IOwnedModel)User).Owner;
                }
                else if (CallContextOrgUnit == null)
                {
                    var query = string.Format("{0}->{1}->{2}{{Key = {3}}}",
                                    ModelTypeManager.GetModelName<IOrgUnit>(),
                                    ModelTypeManager.GetModelName<owns>(),
                                    ModelTypeManager.GetModelName<IUser>(),
                                    Model.GetKey());
                    return ModelQueryEvaluator<IOrgUnit>(query);
                }
                else
                {
                    var query = string.Format("{0}{{Name = '{1}'}}",
                                    ModelTypeManager.GetModelName<IOrgUnit>(),
                                    CallContextOrgUnit);
                    return ModelQueryEvaluator<IOrgUnit>(query);
                }
            }
        }

        public IEnumerable<AuditedChange> Changes { get; private set; }

        public Type ModelType { get; private set; }

        public IModel GetOwner(IModel model)
        {
            if (model is owns)
            {
                return ((owns)model).From;
            }
            else if (model is IOwnedModel)
            {
                if (((IOwnedModel)model).Owner == null)
                {
                    if (model.GlobalKey() == IOrgUnitDefaults.RootOrgUnit.GlobalKey())
                        return null;

                    ((_IOwnedModel)model).Owner = (ModelQueryEvaluator<IAny>(
                        string.Format("{0}{{Key={1}}}",
                            ModelTypeManager.GetModelName(model.ModelType),
                            model.GetKey())) as IOwnedModel)?.Owner;
                }
                return ((IOwnedModel)model).Owner;
            }
            else return null;
        }

        public bool AuditIsLimitedTo(string[] propertyNames)
        {
            return Changes.All(ac => propertyNames.Any(pn => ac.PropertyName == pn));
        }

        public bool AuditIncludes(string[] propertyNames)
        {
            return propertyNames.All(pn => Changes.Any(c => c.PropertyName == pn));
        }

        public bool Contains(Array source, object test)
        {
            foreach (var item in source)
            {
                if ((source == null && test == null) || source.Equals(test))
                    return true;
            }
            return false;
        }

        public bool Contains(string source, string test)
        {
            return source.Contains(test);
        }

        public bool StartsWith(string source, string test)
        {
            return source.StartsWith(test);
        }

        public bool EndsWith(string source, string test)
        {
            return source.EndsWith(test);
        }

        public string Concat(string left, string right)
        {
            return string.Concat(left, right);
        }
        

        public bool HasPermissions(string[] permissions)
        {
            return Permission.TryDemand(permissions);
        }

        public bool IsAuthenticated()
        {
            var identity = SecurityContext.Current.CurrentPrincipal?.Identity;
            if (identity == null) return false;
            return identity.IsAuthenticated;
        }
        public virtual bool IsAuthenticated(IUser user)
        {
            var identity = SecurityContext.Current.CurrentPrincipal?.Identity;
            if (identity == null) return false;
            return identity.IsAuthenticated && identity.Name.Equals(user.Username);
        }

        public virtual bool IsModelType(IModel model, string modelTypeName)
        {
            return ModelTypeManager.GetModelName(model.ModelType).Equals(modelTypeName);
        }

        public virtual bool IsInContainer(IModel model, string containerName)
        {
            var query = string.Format("{0}{{Name = {3}}}->{1}->{2}{{Key = {4}}}",
                                ModelTypeManager.GetModelName<IContainer>(),
                                ModelTypeManager.GetModelName<contains>(),
                                ModelTypeManager.GetModelName(model.ModelType),
                                containerName,
                                model.GetKey());
            return BooleanQueryEvaluator(query);
        }

        public bool IsInOrgUnit(string orgUnitName)
        {
            return IsInOrgUnit(SecurityContext.Current?.ToUser(), orgUnitName);
        }

        public virtual bool IsInOrgUnit(IUser user, string orgUnitName)
        {
            var query = string.Format("{0}{{Name = {3}}}<-{1}<-{2}{{Key = {4}}}",
                                ModelTypeManager.GetModelName<IOrgUnit>(),
                                ModelTypeManager.GetModelName<isMemberOf>(),
                                ModelTypeManager.GetModelName<IUser>(),
                                orgUnitName,
                                user.GetKey());
            return BooleanQueryEvaluator(query);
        }

        public bool IsInRole(string roleName)
        {
            return IsInRole(SecurityContext.Current?.ToUser(), roleName);
        }

        public virtual bool IsInRole(IUser user, string roleName)
        {
            if (user.Username.Equals(SecurityContext.Current?.CurrentPrincipal?.Identity?.Name))
            {
                // if the test user is the current user, just test his claims, as they will contain the user's roles
                var identity = SecurityContext.Current.CurrentPrincipal.Identity as IClaimsIdentity;
                return identity.Claims.Any(c => c.ClaimType.Equals(ClaimTypes.Role) && c.OriginalIssuer.Equals(AppContext.Name) && c.Value.Equals(roleName));
            }
            else
            {
                // not the current user, so we need to hit the database to get them
                var query = string.Format("{0}{{Key = {3}}}->{1}->{2}{{Name = '{4}'}}",
                                    ModelTypeManager.GetModelName<IUser>(),
                                    ModelTypeManager.GetModelName<isMemberOf>(),
                                    ModelTypeManager.GetModelName<IRole>(),
                                    user.GetKey(),
                                    roleName);
                return BooleanQueryEvaluator(query);
            }
        }

        public virtual bool IsOwnedBy(IModel model, string orgUnitName)
        {
            if (Model is IOwnedModel && ((IOwnedModel)Model).Owner != null)
            {
                return ((IOwnedModel)Model).Owner.Name.Equals(orgUnitName);
            }
            else
            {
                var query = string.Format("{0}{{Key = {3}}}<-{1}<-{2}{{Name = '{4}'}}",
                                    ModelTypeManager.GetModelName<IUser>(),
                                    ModelTypeManager.GetModelName<owns>(),
                                    ModelTypeManager.GetModelName<IOrgUnit>(),
                                    model.GetKey(),
                                    orgUnitName);
                return BooleanQueryEvaluator(query);
            }
        }

        public bool IsUserAuthenticated()
        {
            return !User.Username.Equals(IUserDefaults.UnauthorizedUser);
        }

        public bool EdgeExists<T>(IModel from, IModel to) where T : ILink
        {
            if (from == null || to == null) return false;

            var query = string.Format("{0}{{Key = {1}}}->{2}->{3}{{Key = {4}}}",
                ModelTypeManager.GetModelName(from.ModelType),
                from.GetKey(),
                ModelTypeManager.GetModelName(typeof(T)),
                ModelTypeManager.GetModelName(to.ModelType),
                to.GetKey());
            return BooleanQueryEvaluator(query);
        }

        public T GetModel<T>(string name) where T : INamedModel
        {
            var query = string.Format("{0}{{Name = {1}}}",
                            ModelTypeManager.GetModelName<T>(),
                            name);
            return ModelQueryEvaluator<T>(query);
        }

        public ModelList<T> Query<T>(string query, IEnumerable<string> args) where T : IModel
        {
            var qp = this.QueryProviderBuilder.CreateQueryProvider<IAny>();
            var result = qp.Query(string.Format(query, args.Cast<object>().ToArray()));

            if (typeof(T).Implements<IPath>())
            {
                var typedResults = new List<IPath>();
                foreach (var path in result.Cast<IPath>())
                {
                    typedResults.Add(Path.Create(typeof(T).GetGenericArguments()[0], path.Root, path.Nodes, path.Edges));
                }
                return new ModelList<T>(typedResults.Cast<T>().ToList(), result.Offset, result.TotalRecords, result.PageCount, result.PageSize);
            }
            else
            {
                return new ModelList<T>(result.Cast<T>().ToList(), result.Offset, result.TotalRecords, result.PageCount, result.PageSize);
            }
        }

        static object _absCacheItem = new object();
        protected bool BooleanQueryEvaluator(string query)
        {
            var audit = AuditSettings.IsEnabled;
            //var security = ModelSecurityManager.IsEnabled;
            try
            {
                lock (_cachedQueries)
                {
                    if (_cachedQueries.Contains(query))
                    {
                        return (bool)_cachedQueries[query];
                    }
                    else
                    {
                        AuditSettings.IsEnabled = false;
                        //ModelSecurityManager.IsEnabled = false;

                        var cqp = QueryProviderBuilder.CreateQueryProvider<IAny>();
                        var result = cqp.Query(query)?.Count() > 0;
                        _cachedQueries.Add(query + "_abs", _absCacheItem, new CacheItemPolicy() { AbsoluteExpiration = DateTimeOffset.Now.AddMinutes(30) });
                        var cp = new CacheItemPolicy()
                        {
                            SlidingExpiration = TimeSpan.FromMinutes(5)
                        };
                        cp.ChangeMonitors.Add(_cachedQueries.CreateCacheEntryChangeMonitor(new string[] { query + "_abs" }));
                        _cachedQueries.Add(query, result, cp);
                        return result;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
                return false;
            }
            finally
            {
                AuditSettings.IsEnabled = audit;
                //ModelSecurityManager.IsEnabled = security;
            }
        }

        protected T ModelQueryEvaluator<T>(string query) where T : IModel
        {
            try
            {
                lock (_cachedQueries)
                {
                    if (_cachedQueries.Contains(query))
                    {
                        return (T)_cachedQueries[query];
                    }
                    else
                    {
                        var cqp = QueryProviderBuilder.CreateQueryProvider<IAny>();
                        var result = cqp.Query(query).OfType<T>().FirstOrDefault();
                        if (result != null)
                        {
                            _cachedQueries.Add(query + "_abs", _absCacheItem, new CacheItemPolicy() { AbsoluteExpiration = DateTimeOffset.Now.AddMinutes(30) });
                            var cp = new CacheItemPolicy()
                            {
                                SlidingExpiration = TimeSpan.FromMinutes(5)
                            };
                            cp.ChangeMonitors.Add(_cachedQueries.CreateCacheEntryChangeMonitor(new string[] { query + "_abs" }));
                            _cachedQueries.Add(query, result, cp);
                        }
                        return result;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
                return default(T);
            }
        }

        public string ToTempFile(string base64Bytes)
        {
            using (var tmp = System.IO.File.Create(System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetTempFileName())))
            {
                StreamHelper.Copy(Convert.FromBase64String(base64Bytes), tmp);
                return tmp.Name;
            }
        }

        public SizeF MeasureText(string text, string fontName, int fontSizeInPoints,
            float maxWidthInPixels = 0f,
            float lineHeightInPixels = 0f,
            bool isBold = false,
            bool isItalic = false,
            bool isUnderline = false)
        {
            var g = Graphics.FromHwnd(IntPtr.Zero);
            var style = FontStyle.Regular;
            if (isBold)
            {
                style |= FontStyle.Bold;
            }
            if (isItalic)
            {
                style |= FontStyle.Italic;
            }
            if (isUnderline)
            {
                style |= FontStyle.Underline;
            }

            var font = new Font(new FontFamily(fontName), fontSizeInPoints, style);

            if (lineHeightInPixels <= 0f)
            {
                lineHeightInPixels = (float)font.Height;
            }

            int charsFitted, linesFitted;
            var size = g.MeasureString(text, font, new SizeF(maxWidthInPixels, float.MaxValue), StringFormat.GenericTypographic, out charsFitted, out linesFitted);
            return new SizeF(size.Width, lineHeightInPixels * linesFitted);
        }

        public string Serialize(object item)
        {
            return item.ToJson();
        }
    }
}
