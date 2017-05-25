using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Common.DI;
using StructureMap;
using Common.Web.Handlers;
using Suffuz.Handlers;
using Data.Core;
using Data.ArangoDB;
using Newtonsoft.Json.Serialization;
using Data.Core.Serialization.Json;
using Data.Core.ComponentModel;
using Altus.Suffūz.Serialization;
using Data.Core.Linq;
using Data.Core.Serialization.Binary;
using Common.Security;
using Common.Auditing;
using Data.Core.Security;
using Data.Core.Evaluation;
using Data.Core.Auditing;
using Shs.Search.Security;

namespace Suffuz.DI
{
    public class SearchRegistry : Common.DI.Registry
    {
        public override Common.DI.IContainer Initialize()
        {
            InitializeSerializers();
            
            return new Common.DI.StructureMapContainer(new Container(c =>
            {
                InitializeModelConverters(c);
                c.For<IHttpTokenAuthenticator>().Use<SHSServerTokenAuthenticator>();
                c.For<IHttpHeaderAuthTokenBuilder>().Use<SHSHttpHeaderAuthTokenBuilder>();
                c.For<ITokenStore>().Use<NonPersistentTokenStore>();
                c.For<ILocateHandlers>().Use<SearchHandlerLocator>();
                c.For<IModelListProviderBuilder>().Use<ArangoProviderBuilder>();
                c.For<IModelPersistenceProviderBuilder>().Use<ArangoProviderBuilder>();
                c.For<IModelQueryProviderBuilder>().Use<ArangoProviderBuilder>();
                c.For<IContractResolver>().Use<ContractResolver>();
                c.For<IModelQueueProviderBuilder>().Use<ArangoProviderBuilder>();
                c.For<IIdentityProvider>().Use<IdentityProvider>();
                c.For<IUserAuthorizationProvider>().Use<IdentityProvider>();
                c.For<IDataContextInitializer>().Use<DataContextInitializer>();
                c.For<IAuditer>().Use<Auditer>();
                c.For<IHttpAuditScopeTokenReader>().Use<HttpAuditScopeTokenReader>();
                c.For<IOrgUnitInitializer>().Use<DefaultOrgUnitInitializer>();
                c.For<IRuleCompiler>().Use<RuleCompiler<Runtime>>();
                c.For<IRuntimeBuilder>().Use<RuntimeBuilder>();
                c.For<IDataAccessSecurityContextRuleProvider>().Use<DataAccessSecurityContextServerRuleProvider>();
            }));
        }
        private void InitializeSerializers()
        {
            SerializationContext.Instance.TextEncoding = UTF8Encoding.UTF8;
            SerializationContext.Instance.SetSerializer<DateTime, Iso8601DateTimeConverter>(StandardFormats.JSON);
            SerializationContext.Instance.SetSerializer<DeleteExpression, DeleteExpressionConverter>(StandardFormats.JSON);
            SerializationContext.Instance.SetSerializer<SaveExpression, SaveExpressionConverter>(StandardFormats.JSON);
            SerializationContext.Instance.SetSerializer<PredicateExpression, ExpressionConverter>(StandardFormats.JSON);
            SerializationContext.Instance.SetSerializer<SortExpression, SortConverter>(StandardFormats.JSON);
            SerializationContext.Instance.SetSerializer<IModel, ModelSerializer>(StandardFormats.JSON);
            SerializationContext.Instance.SetSerializer<IModelList, ModelListSerializer>(StandardFormats.JSON);
            SerializationContext.Instance.SetSerializer<SortExpression[], SortExpressionArraySerializer>(StandardFormats.BINARY);
            SerializationContext.AddSerializer<IEnumerableModelSerializer>(StandardFormats.BINARY);
        }
        private void InitializeModelConverters(ConfigurationExpression c)
        {
            ModelTypeConverter.ModelBaseType = typeof(ModelBase);
            ModelTypeConverter.LinkBaseType = typeof(LinkBase);
            ModelTypeConverter.TrackedModelBaseType = typeof(TrackedModelBase);
        }
    }
}
