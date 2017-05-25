using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DI
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = true)]
    public class PluginAttribute : Attribute
    {
        private string _name;
        private string _description;
        private string _type;
        private string _targetType;
        private Type _ctype;
        private Type _ttype;
        private string[] _dependencies = new string[0];
        private object[] _ctorArgs = new object[0];

        public PluginAttribute() { Enabled = true; Reflect = true; }

        public int Id { get; set; }

        public string Name
        {
            get
            {
                if (string.IsNullOrEmpty(_name))
                {
                    return _type;
                }
                else
                {
                    return _name;
                }
            }
            set { _name = value; }
        }

        public string Description
        {
            get { return _description; }
            set { _description = value; }
        }

        public string Plugin
        {
            get { return _type; }
            set
            {

                _type = value;
            }
        }

        public bool IsValid { get; private set; }
        public bool IsValidated { get; private set; }

        public Type PluginType
        {
            get
            {
                if (!IsValidated)
                {
                    try
                    {
                        _ctype = TypeHelper.GetType(_type);
                        _type = _ctype.AssemblyQualifiedName;
                        IsValid = true;
                    }
                    catch
                    {
                        IsValid = false;
                    }
                    finally
                    {
                        IsValidated = true;
                    }
                }
                return _ctype;
            }
            set
            {
                _ctype = value;
                if (value == null)
                {
                    _type = null;
                    IsValidated = false;
                    IsValid = false;
                }
                else
                {
                    _type = value.AssemblyQualifiedName;
                    IsValidated = true;
                    IsValid = true;
                }
            }
        }

        public Type TargetType
        {
            get
            {
                if (!IsValidated)
                {
                    try
                    {
                        _ttype = TypeHelper.GetType(_targetType);
                        _targetType = _ttype.AssemblyQualifiedName;
                        IsValid = true;
                    }
                    catch
                    {
                        IsValid = false;
                    }
                    finally
                    {
                        IsValidated = true;
                    }
                }
                return _ttype;
            }
            set
            {
                _ttype = value;
                if (value == null)
                {
                    _targetType = null;
                    IsValidated = false;
                    IsValid = false;
                }
                else
                {
                    _targetType = value.AssemblyQualifiedName;
                    IsValidated = true;
                    IsValid = true;
                }
            }
        }
        /// <summary>
        /// Gets/sets the list of Component Names that should be loaded prior to
        /// loading this component
        /// </summary>

        public string[] Dependencies
        {
            get
            {
                return _dependencies;
            }
            set
            {
                _dependencies = value;
            }
        }

        public object[] CtorArgs
        {
            get { return _ctorArgs; }
            set { _ctorArgs = value; }
        }

        private string DependenciesString
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                foreach (string s in this.Dependencies)
                {
                    if (sb.Length > 0)
                        sb.Append(", ");
                    sb.Append(s);
                }
                return sb.ToString();
            }
            set
            {
                if (value != null)
                    this.Dependencies = value.Split(',');
            }
        }

        public bool Enabled { get; set; }

        /// <summary>
        /// Allows for components to be sorted relative to each other.  Bigger numbers are executed before littler numbers.
        /// </summary>
        public ushort Priority { get; set; }

        public object Instance { get; set; }

        public bool Reflect { get; set; }

        public override string ToString()
        {
            if (Name.Equals(Plugin)) return Name;
            return string.Format("{0} [{1}]",
                Name,
                Plugin);
        }
    }
}
