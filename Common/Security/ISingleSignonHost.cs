using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Security
{
    public interface ISingleSignonHost
    {
        /// <summary>
        /// Gets the channel Id for a specific child process id
        /// <returns>the channel id for the child</returns>
        /// </summary>
        string CreateChannel(string childId);
        /// <summary>
        /// Removes the child process from ipc communications
        /// </summary>
        /// <param name="childId"></param>
        void RemoveChannel(string childId);
        /// <summary>
        /// Should be called after the child process has been created by the parent process
        /// </summary>
        void ChildCreated(string childId);
        /// <summary>
        /// queries all SSO clients to ensure that they are in a state where login can occur.  If any client responds false, 
        /// this method returns false.
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        bool TryClientLogin(string username);
        /// <summary>
        /// queries all SSO clients to ensure that they are in a state where logout can occur.  If any client responds false, 
        /// this method returns false.
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        bool TryClientLogout(string username);
        /// <summary>
        /// Called after a successful login, signaling all clients that a new login took place.
        /// </summary>
        /// <param name="username"></param>
        void LoginSuccessful(string username);
        /// <summary>
        /// Called after a successful logout, signaling all clients that a logout took place.
        /// </summary>
        /// <param name="username"></param>
        void LogoutSuccessful(string username);
        /// <summary>
        /// Signals all clients that the current security token has been updated.
        /// </summary>
        /// <param name="username"></param>
        void TokenUpdated(string username, string encryptionKey);
    }
}
