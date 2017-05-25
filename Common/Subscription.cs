using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public abstract class Subscription : IDisposable
    {
        protected Action<Subscription> OnDisposed { get; set; }

        public abstract void Raise(EventArgs args);

        bool disposed = false;
        public void Dispose()
        {
            if (!disposed)
            {
                OnDisposed(this);
                disposed = true;
            }
        }
    }

    public class Subscription<T> : Subscription where T : EventArgs
    {
        private Action<T> callback;

        public Subscription(Action<T> callback, Action<Subscription> onDisposed)
        {
            this.callback = callback;
            this.OnDisposed = onDisposed;
        }

        public override void Raise(EventArgs args)
        {
            Raise((T)args);
        }

        public void Raise(T args)
        {
            callback(args);
        }
    }
}
