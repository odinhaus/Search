using Common.Security;
using Data.Core.Security;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Evaluation
{
    public interface IRuntime
    {
        string @CustomArg { get; }
        IOrgUnit @RootOrgUnit { get; }
        /// <summary>
        /// Gets the current caller's user record.
        /// </summary>
        IUser @User { get; }
        /// <summary>
        /// Gets the current data model for the call.
        /// </summary>
        IModel @Model { get; }
        /// <summary>
        /// Gets the To value for a Link model type.  This value will be null if the current model is not an edge type.
        /// </summary>
        IModel @To { get; }
        /// <summary>
        /// Gets the From value for a Link model type.  This value will be null if the current model is not an edge type.
        /// </summary>
        IModel @From { get; }
        /// <summary>
        /// Gets the time of the call.
        /// </summary>
        DateTime @Now { get; }
        /// <summary>
        /// Gets the org unit that owns the current model.
        /// </summary>
        IOrgUnit @ModelOrgUnit { get; }
        /// <summary>
        /// Gets the action being performed by the user
        /// </summary>
        DataActions @Action { get; }
        string @ModelClass { get; }
        Type @ModelType { get; }
        string @ToClass { get; }
        string @FromClass { get; }
        /// <summary>
        /// Gets the OrgUnit Name specified by the call context of the API's URI.  This value is used to scope 
        /// data persistence operations to assign ownership of items to a particular org unit when saving. 
        /// Models may only be owned by a single OrgUnit.
        /// </summary>
        string @CallContextOrgUnit { get; }
        
        bool IsModelType(IModel model, string modeltypeName);
        bool IsOwnedBy(IModel model, string ownerName);
        bool IsInContainer(IModel model, string containerName);
        bool IsAuthenticated();
        bool IsInOrgUnit(string orgUnitName);
        bool IsInRole(string roleName);
        bool HasPermissions(string[] permissions);
        bool IsAuthenticated(IUser user);
        bool IsInOrgUnit(IUser user, string orgUnitName);
        bool IsInRole(IUser user, string roleName);
        bool Contains(string source, string test);
        bool Contains(Array source, object test);
        bool StartsWith(string source, string test);
        bool EndsWith(string source, string test);
        bool AuditIsLimitedTo(string[] fields);
        bool AuditIncludes(string[] fields);
        bool EdgeExists<T>(IModel from, IModel to) where T : ILink;
        T GetModel<T>(string name) where T : INamedModel;
        IModel GetOwner(IModel model);
        string Concat(string left, string right);
        ModelList<T> Query<T>(string query, IEnumerable<string> args) where T : IModel;
        string ToTempFile(string base64Bytes);
        SizeF MeasureText(string text, string fontName, int fontSize,
            float maxWidth = 0f,
            float lineHeight = 0f,
            bool isBold = false,
            bool isItalic = false,
            bool isUnderline = false);

        string Serialize(object item);
    }
}
