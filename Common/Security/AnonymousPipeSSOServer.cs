using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Common.Security
{
    public class AnonymousPipeSSOServer : ISingleSignonHost
    {
        static Dictionary<string, Channel> _channels = new Dictionary<string, Channel>();

        static AnonymousPipeSSOServer()
        {
            AppContext.Current.Subscribe<TokenUpdatedEventArgs>((e) =>
            {
                var server = new AnonymousPipeSSOServer();
                server.TokenUpdated(e.Username, e.EncryptionKey);
            });
        }

        public string CreateChannel(string childId)
        {
            Channel channel;
            lock(_channels)
            {
                if (!_channels.TryGetValue(childId, out channel))
                {
                    channel = new Channel(this, childId);
                    _channels.Add(childId, channel);
                }
            }
            return channel.ChannelId;
        }

        public void ChildCreated(string childId)
        {
            Channel channel;
            lock(_channels)
            {
                if (_channels.TryGetValue(childId, out channel))
                {
                    channel.DisposeLocalHandles();
                }
            }
        }

        public void LoginSuccessful(string username)
        {
            lock(_channels)
            {
                foreach(var channel in _channels.Values)
                {
                    channel.LoginSuccessful(username);
                }
            }
            AppContext.Current.Raise(new LoginSuccessfulEventArgs());
        }

        public void LogoutSuccessful(string username)
        {
            lock (_channels)
            {
                foreach (var channel in _channels.Values)
                {
                    channel.LogoutSuccessful(username);
                }
            }
            AppContext.Current.Raise(new LogoutSuccessfulEventArgs());
        }

        public void TokenUpdated(string username, string encryptionKey)
        {
            lock (_channels)
            {
                foreach (var channel in _channels.Values)
                {
                    channel.TokenUpdated(username, encryptionKey);
                }
            }
        }

        public bool TryClientLogin(string username)
        {
            lock (_channels)
            {
                foreach (var channel in _channels.Values)
                {
                    if (!channel.TryClientLogin(username))
                        return false;
                }
            }
            return true;
        }

        public bool TryClientLogout(string username)
        {
            lock (_channels)
            {
                foreach (var channel in _channels.Values)
                {
                    if (!channel.TryClientLogout(username))
                        return false;
                }
            }
            return true;
        }

        public void RemoveChannel(string childId)
        {
            Channel channel;
            lock (_channels)
            {
                if (_channels.TryGetValue(childId, out channel))
                {
                    channel.Dispose();
                    _channels.Remove(childId);
                }
            }
        }

        private class Channel : IDisposable
        {
            PipeStream _pipeOut;
            PipeStream _pipeIn;
            Thread _ssoReader;
            ManualResetEvent _qryEvt = new ManualResetEvent(false);
            bool _qryResult = false;

            public Channel(ISingleSignonHost parent, string childId)
            {
                this.Parent = parent;
                this.ChildId = childId;
                _pipeIn = new AnonymousPipeServerStream(PipeDirection.In, System.IO.HandleInheritability.Inheritable);
                _pipeOut = new AnonymousPipeServerStream(PipeDirection.Out, System.IO.HandleInheritability.Inheritable);
                ChannelId = string.Format("{0}|{1}", ((AnonymousPipeServerStream)_pipeIn).GetClientHandleAsString(), ((AnonymousPipeServerStream)_pipeOut).GetClientHandleAsString());
                BeginRead();
            }

            public void LoginSuccessful(string username)
            {
                SendEvent(username, SSOMessages.EVT_LOGIN_SUCCESSFUL);
            }

            public void LogoutSuccessful(string username)
            {
                SendEvent(username, SSOMessages.EVT_LOGOUT_SUCCESSFUL);
            }

            public void TokenUpdated(string username, string encryptionKey)
            {
                SendTokenUpdatedEvent(username, SSOMessages.EVT_TOKEN_UPDATE, encryptionKey);
            }

            public bool TryClientLogin(string username)
            {
                return SendClientQuery(username, SSOMessages.QRY_TRY_LOGIN);
            }


            public bool TryClientLogout(string username)
            {
                return SendClientQuery(username, SSOMessages.QRY_TRY_LOGOUT);
            }

            private bool SendClientQuery(string username, string command)
            {
                using (var sw = new StreamWriter(_pipeOut, UTF8Encoding.UTF8, 1024, true))
                {
                    IsWaiting = true;
                    sw.AutoFlush = true;
                    sw.WriteLine(string.Format("{0}|{1}", username, command));
                    _pipeOut.WaitForPipeDrain();
                }
                _qryEvt.WaitOne();
                _qryEvt.Reset();
                return _qryResult;
            }

            private void SendEvent(string username, string command)
            {
                using (var sw = new StreamWriter(_pipeOut, UTF8Encoding.UTF8, 1024, true))
                {
                    sw.AutoFlush = true;
                    sw.WriteLine(string.Format("{0}|{1}", username, command));
                    _pipeOut.WaitForPipeDrain();
                }
            }

            private void SendTokenUpdatedEvent(string username, string command, string encryptionKey)
            {
                using (var sw = new StreamWriter(_pipeOut, UTF8Encoding.UTF8, 1024, true))
                {
                    sw.AutoFlush = true;
                    sw.WriteLine(string.Format("{0}|{1}|{2}", username, command, encryptionKey));
                    _pipeOut.WaitForPipeDrain();
                }
            }


            private void BeginRead()
            {
                _ssoReader = new Thread(new ThreadStart(() =>
                {
                    using (var sr = new StreamReader(_pipeIn, UTF8Encoding.UTF8, true, 1024, true))
                    {
                        var signal = string.Empty;
                        while ((signal = sr.ReadLine()) != null)
                        {
                            ProcessSignal(signal);
                        }
                    }
                }));
                _ssoReader.IsBackground = true;
                _ssoReader.Name = "Single Signon IPC Reader for Child " + ChildId;
                _ssoReader.Start();
            }

            private void ProcessSignal(string signal)
            {
                if (IsWaiting)
                {
                    IsWaiting = false;
                    var split = signal.Split('|');
                    var username = split[0];
                    var cmd = split[1];
                    var result = bool.Parse(split[2]);

                    _qryResult = result;

                    _qryEvt.Set();
                }
                else
                {
                    var split = signal.Split('|');
                    var username = split[0];
                    var action = split[1];

                    switch (action)
                    {
                        case SSOMessages.CMD_TRY_LOGIN:
                            {
                                if (Parent.TryClientLogin(username))
                                {
                                    var args = new TryLoginEventArgs(username);
                                    AppContext.Current.Raise(args);
                                    if (!args.Cancel)
                                    {
                                        Parent.LoginSuccessful(username);
                                    }
                                }
                                
                                break;
                            }
                        case SSOMessages.CMD_TRY_LOGOUT:
                            {
                                if (Parent.TryClientLogout(username))
                                {
                                    var args = new TryLogoutEventArgs(username);
                                    AppContext.Current.Raise(args);
                                    if (!args.Cancel)
                                    {
                                        Parent.LogoutSuccessful(username);
                                    }
                                }

                                break;
                            }
                    }
                }
            }

            public string ChildId { get; private set; }
            public string ChannelId { get; private set; }
            public bool IsWaiting { get; private set; }
            public ISingleSignonHost Parent { get; private set; }

            bool _localHandlesDisposed = false;
            internal void DisposeLocalHandles()
            {
                if (!_localHandlesDisposed)
                {
                    ((AnonymousPipeServerStream)_pipeIn).DisposeLocalCopyOfClientHandle();
                    ((AnonymousPipeServerStream)_pipeOut).DisposeLocalCopyOfClientHandle();
                }
            }

            #region IDisposable
            protected bool _disposed = false;

            protected virtual void Dispose(bool disposing)
            {
                if (_disposed) return;

                if (disposing)
                {
                    // release managed references here
                    OnDisposeManaged();
                }

                // release unmanged references here
                this.OnDisposeUnmanaged();

                _disposed = true;
            }

            /// <summary>
            /// Closes the storage connection and disposes of all tracked items
            /// </summary>
            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected virtual void OnDisposeManaged()
            {
                _pipeIn.Dispose();
                _pipeOut.Dispose();
            }

            protected virtual void OnDisposeUnmanaged()
            {

            }

            #endregion
        }
    }
}
