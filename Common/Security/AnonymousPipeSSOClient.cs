using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Common.Security
{
    public class LoginSuccessfulEventArgs : EventArgs
    { }

    public class LogoutSuccessfulEventArgs : EventArgs
    { }

    public class TokenUpdatedEventArgs : EventArgs
    {
        public TokenUpdatedEventArgs(string username, string key)
        {
            this.Username = username;
            this.EncryptionKey = key;
        }

        public string EncryptionKey { get; private set; }
        public string Username { get; private set; }
    }

    public class QueryTryLoginEventArgs : CancelEventArgs
    {

    }

    public class QueryTryLogoutEventArgs : CancelEventArgs
    {

    }

    public class TryLoginEventArgs : CancelEventArgs
    {
        public TryLoginEventArgs(string username)
        {
            this.Username = username;
        }

        public string Username { get; private set; }
    }

    public class TryLogoutEventArgs : CancelEventArgs
    {
        public TryLogoutEventArgs(string username)
        {
            this.Username = username;
        }

        public string Username { get; private set; }
    }

    public class SSOMessages
    {
        public const string CMD_TRY_LOGIN = "TRY_LOGIN";
        public const string CMD_TRY_LOGOUT = "TRY_LOGOUT";

        public const string EVT_LOGIN_SUCCESSFUL = "LOGIN_SUCCESSFUL";
        public const string EVT_LOGOUT_SUCCESSFUL = "LOGOUT_SUCCESSFUL";
        public const string EVT_TOKEN_UPDATE = "TOKEN_UPDATED";

        public const string QRY_TRY_LOGOUT = "TRY_LOGOUT";
        public const string QRY_TRY_LOGIN = "TRY_LOGIN";

        public const string QRY_TRY_LOGOUT_RESPONSE = "TRY_LOGOUT_RESPONSE";
        public const string QRY_TRY_LOGIN_RESPONSE = "TRY_LOGIN_RESPONSE";
    }

    public class AnonymousPipeSSOClient : ISingleSignonClient, IDisposable
    {
        PipeStream _pipeIn;
        PipeStream _pipeOut;
        Thread _ssoReader;

        ManualResetEvent _cmdEvt = new ManualResetEvent(false);
        bool _cmdResult = false;

        public void Initialize(string channelId)
        {
            var split = channelId.Split('|');
            _pipeOut = new AnonymousPipeClientStream(PipeDirection.Out, split[0]);
            _pipeIn = new AnonymousPipeClientStream(PipeDirection.In, split[1]);
            BeginRead();
        }

        private void BeginRead()
        {
            _ssoReader = new Thread(new ThreadStart(() =>
            {
                using (var sr = new StreamReader(_pipeIn, UTF8Encoding.UTF8, true, 1024, true))
                {
                    var signal = string.Empty;
                    while((signal = sr.ReadLine()) != null)
                    {
                        ProcessSignal(signal);
                    }
                }
            }));
            _ssoReader.IsBackground = true;
            _ssoReader.Name = "Single Signon IPC Reader";
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

                _cmdResult = result;

                _cmdEvt.Set();
            }
            else
            {
                var split = signal.Split('|');
                var username = split[0];
                var action = split[1];

                switch (action)
                {
                    case SSOMessages.EVT_LOGIN_SUCCESSFUL:
                        {
                            AppContext.Current.Raise(new LoginSuccessfulEventArgs());
                            break;
                        }
                    case SSOMessages.EVT_LOGOUT_SUCCESSFUL:
                        {
                            AppContext.Current.Raise(new LogoutSuccessfulEventArgs());
                            break;
                        }
                    case SSOMessages.EVT_TOKEN_UPDATE:
                        {
                            AppContext.Current.Raise(new TokenUpdatedEventArgs(username, split[2]));
                            break;
                        }
                    case SSOMessages.QRY_TRY_LOGIN:
                        {
                            var args = new QueryTryLoginEventArgs();
                            AppContext.Current.Raise(args);
                            SendHostResponse(username, SSOMessages.QRY_TRY_LOGIN_RESPONSE, (!args.Cancel).ToString());
                            break;
                        }
                    case SSOMessages.QRY_TRY_LOGOUT:
                        {
                            var args = new QueryTryLogoutEventArgs();
                            AppContext.Current.Raise(args);
                            SendHostResponse(username, SSOMessages.QRY_TRY_LOGOUT_RESPONSE, (!args.Cancel).ToString());
                            break;
                        }
                }
            }
        }

        public bool TryHostLogin(string username)
        {
            return SendHostCommand(username, SSOMessages.CMD_TRY_LOGIN);
        }

        public bool TryHostLogout(string username)
        {
            return SendHostCommand(username, SSOMessages.CMD_TRY_LOGOUT);
        }

        public bool IsWaiting { get; private set; }

        private void SendHostResponse(string username, string command, string result)
        {
            using (var sw = new StreamWriter(_pipeOut, UTF8Encoding.UTF8, 1024, true))
            {
                sw.AutoFlush = true;
                sw.WriteLine(string.Format("{0}|{1}|{2}", username, command, result));
                _pipeOut.WaitForPipeDrain();
            }
        }

        private bool SendHostCommand(string username, string command)
        {
            using (var sw = new StreamWriter(_pipeOut, UTF8Encoding.UTF8, 1024, true))
            {
                IsWaiting = true;
                sw.AutoFlush = true;
                sw.WriteLine(string.Format("{0}|{1}", username, command));
                _pipeOut.WaitForPipeDrain();
            }
            _cmdEvt.WaitOne();
            _cmdEvt.Reset();
            return _cmdResult;
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
