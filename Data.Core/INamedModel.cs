using Altus.Suffūz.Serialization.Binary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core
{
    public interface INamedModel : IModel
    {
        [BinarySerializable(-30)]
        [Searchable]
        [Unique]
        /// <summary>
        /// Gets/sets the unique name for the  Model.  Models are saved with global scope, and globally unique names, so application 
        /// designers should determine a role naming scheme to enure global uniqueness for multi-tenant application, such as 
        /// prefixing the model name with the org-unit name, or similar namescoping techniques.
        /// </summary>
        string Name { get; set; }
    }
}
