using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Common.DI;

namespace Common.Application
{
    public abstract class InitializablePlugin : System.ComponentModel.Component, IInitialize
    {
        #region IInitialize Members

        public virtual void Initialize(string name, params string[] args)
        {
            Arguments = args;
            IsEnabled = true;
            Name = name;
            this.IsInitialized = this.OnInitialize(args);
        }

        protected abstract bool OnInitialize(params string[] args);

        public bool IsInitialized
        {
            get;
            protected set;
        }

        public bool IsRegistered
        {
            get;
            protected set;
        }

        public bool IsEnabled { get; protected set; }
        public string Name { get; protected set; }

        #endregion

        protected bool _disposed = false;
        protected override void Dispose(bool disposing)
        {
            if (!this._disposed)
            {
                this._disposed = true;
                if (disposing)
                {
                    this.OnDispose();
                }
            }
            base.Dispose(disposing);
        }

        protected virtual void OnDispose()
        {
        }

        public abstract void Register(IContainerMappings mappings);

        public string[] Arguments { get; protected set; }
    }
}
