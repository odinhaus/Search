using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Scripting
{
    public class VariableScope : IDisposable, IEnumerable<Expression>
    {
        bool _disposed = false;
        List<VariableScope> _children = new List<VariableScope>();
        Dictionary<string, Expression> _variables = new Dictionary<string, Expression>();

        [ThreadStatic]
        static VariableScope _root = new VariableScope(true);
        [ThreadStatic]
        static VariableScope _current;

        VariableScope _parent;

        /// <summary>
        /// Creates a new variable scope peer child node under the Current scope, and assigns the Current scope to this instance
        /// </summary>
        public VariableScope()
        {
            _parent = VariableScope.Current;
            _parent._children.Add(this);
            _current = this;
        }

        private VariableScope(bool isRoot)
        {
            if (isRoot)
            {
                _parent = null;
            }
            _current = this;
        }

        /// <summary>
        /// Switches the Current scope to the Parent of this scope, but DOES NOT remove itself from the current scope tree
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _current = _parent;
            }
        }

        public Expression this[string key]
        {
            get
            {
                return _variables[key];
            }
        }

        public IEnumerable<VariableScope> Children
        {
            get
            {
                return _children.AsReadOnly();
            }
        }

        public VariableScope Parent
        {
            get
            {
                return _parent;
            }
        }

        public static VariableScope Root
        {
            get
            {
                if (_root == null)
                {
                    _root = new VariableScope(true);
                }
                return _root;
            }
        }

        public bool IsRoot { get { return _parent == null; } }

        /// <summary>
        /// Creates a child scope to the current scope, and changes scope to the new child
        /// </summary>
        public static void Push()
        {
            new VariableScope();
        }

        /// <summary>
        /// Removes all child scopes from the current scope, and changes scope to the parent of the current scope, removing the current scope 
        /// from the children of the parent scope
        /// </summary>
        public static void Pop()
        {
            if (_current == null) return;
            _current._children.Clear();
            _current._children.Remove(_current);
            if (!_current.IsRoot)
                _current = _current._parent;
        }

        /// <summary>
        /// Removes all variable scopes, resetting to an empty root scope
        /// </summary>
        public static void Clear()
        {
            if (Root != null)
            {
                Root._children.Clear();
                _current = _root;
            }
            Current._variables.Clear();
        }

        public static VariableScope Current
        {
            get
            {
                if (_current == null)
                {
                    _current = Root;
                }
                return _current;
            }
        }

        public void Add(string name, Expression variable)
        {
            _variables.Add(name, variable);
        }

        public void Remove(string name)
        {
            _variables.Remove(name);
        }

        public static Expression Search(string name)
        {
            return SearchUp(name, Current);
        }

        private static Expression SearchUp(string name, VariableScope scope)
        {
            if (scope == null || string.IsNullOrEmpty(name)) return null;
            Expression found;
            if (scope._variables.TryGetValue(name, out found))
            {
                return found;
            }
            else
            {
                return SearchUp(name, scope._parent);
            }
        }

        public IEnumerator<Expression> GetEnumerator()
        {
            return _variables.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
