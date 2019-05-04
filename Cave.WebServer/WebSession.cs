using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Cave.Auth;
using Cave.Collections;
using Cave.Data;
using Cave.IO;

namespace Cave.Web
{
    /// <summary>
    /// Provides session data for embedded web server.
    /// </summary>
    public class WebSession
    {
        #region static class

        /// <summary>Loads the session.</summary>
        /// <param name="request">The request.</param>
        /// <returns>Returns a new session instance.</returns>
        public static WebSession LoadSession(WebRequest request)
        {
            var userSession = new UserSession()
            {
                Expiration = DateTime.UtcNow + request.Server.SessionTimeout,
                Source = request.SourceAddress,
                UserAgent = request.UserAgent,
            };

            // try load session=value from header, parse and check > 0
            {
                if (request.Headers.TryGetValue("session", out string value) && long.TryParse(value, out long sessionID) && sessionID > 0)
                {
                    userSession.ID = sessionID;
                }
            }

            // try load cookie
            {
                if (request.Headers.TryGetValue("cookie", out string cookie))
                {
                    foreach (string part in cookie.Split(';'))
                    {
                        var opt = Option.Parse(part);
                        if (opt.Name.Trim() == "Session")
                        {
                            // load existing
                            if (long.TryParse(opt.Value, out long sessionID) && sessionID > 0)
                            {
                                userSession.ID = sessionID;
                                break;
                            }
                        }
                    }
                }
            }

            // check load session
            if (userSession.ID != 0)
            {
                Search sessionSearch =
                    Search.FieldEquals(nameof(Cave.Auth.UserSession.ID), userSession.ID) &
                    Search.FieldEquals(nameof(Cave.Auth.UserSession.UserAgent), userSession.UserAgent);

                if (request.Server.RequireSessionSourceCheck)
                {
                    var ip = IPAddress.Parse(userSession.Source);
                    if (ip.AddressFamily == AddressFamily.InterNetworkV6 && !ip.ToString().Contains("."))
                    {
                        byte[] bytes = ip.GetAddressBytes();
                        if (!BitConverter.IsLittleEndian)
                        {
                            for (int i = 0; i < 8; i++)
                            {
                                bytes[i] = 0;
                            }
                        }
                        else
                        {
                            for (int i = 8; i < 16; i++)
                            {
                                bytes[i] = 0;
                            }
                        }
                        ip = new IPAddress(bytes);
                        string result = ip.ToString();
                        string source = result.StartsWith(":") ? userSession.Source : ip.ToString().TrimEnd(':') + "%";
                        sessionSearch &= Search.FieldLike(nameof(Cave.Auth.UserSession.Source), source);
                    }
                    else
                    {
                        sessionSearch &= Search.FieldEquals(nameof(Cave.Auth.UserSession.Source), userSession.Source);
                    }
                }

                // load old session
                userSession.ID = 0;
                IList<UserSession> sessions = request.Server.AuthTables.UserSessions.GetStructs(sessionSearch);
                foreach (UserSession session in sessions)
                {
                    // session expired or session already loaded ? (duplicate)
                    if (session.IsExpired() || userSession.ID != 0)
                    {
                        request.Server.AuthTables.UserSessions.TryDelete(session.ID);
                        Trace.TraceInformation("{0}: exired and deleted", session);
                        continue;
                    }
                    userSession = session;
                }
                if (userSession.ID > 0 && userSession.Source != request.SourceAddress)
                {
                    Trace.TraceInformation("{0}: source address changed to {1}", userSession, request.SourceAddress);
                }
            }
            if (userSession.ID == 0)
            {
                // save created session
                if (request.Server.SessionMode != WebServerSessionMode.None)
                {
                    while (true)
                    {
                        userSession.ID = DefaultRNG.Int64;
                        if (userSession.ID < 0)
                        {
                            continue;
                        }

                        if (request.Server.AuthTables.UserSessions.TryInsert(userSession))
                        {
                            break;
                        }
                    }
                }
            }
            else
            {
                // update at db
                userSession.Expiration = DateTime.UtcNow + request.Server.SessionTimeout;
                Task.Factory.StartNew(() =>
                {
                    request.Server.AuthTables.UserSessions.TryUpdate(userSession);
                    request.Server.AuthTables.UserSessions.TryDelete(Search.FieldSmaller(nameof(Cave.Auth.UserSession.Expiration), DateTime.UtcNow));
                });
            }
            return new WebSession(request.Server, userSession);
        }

        #endregion

        readonly WebServer server;
        User user;

        /// <summary>Initializes a new instance of the <see cref="WebSession" /> class.</summary>
        /// <param name="server">The server.</param>
        /// <param name="userSession">The user session.</param>
        public WebSession(WebServer server, UserSession userSession)
        {
            this.server = server;
            UserSession = userSession;
        }

        #region public functions

        /// <summary>Expires this instance.</summary>
        public void Expire()
        {
            server.AuthTables.UserSessionData.TryDelete(nameof(UserSessionData.UserSessionID), UserSession.ID);
            server.AuthTables.UserSessions.TryDelete(UserSession.ID);
            UserSession session = UserSession;
            session.Expiration = default(DateTime);
            UserSession = session;
        }

        /// <summary>Sets the authentication.</summary>
        /// <param name="user">The user.</param>
        /// <param name="flags">Used internally to define local host usage.</param>
        /// <exception cref="WebServerException">
        /// Authentication not allowed!
        /// or
        /// Session belongs to another user!.
        /// </exception>
        public void SetAuthentication(User user, UserSessionFlags flags)
        {
            if (user.ID <= 0)
            {
                throw new ArgumentNullException(nameof(user));
            }

            UserSession userSession = UserSession;
            userSession.Flags = flags;
            if (userSession.ID <= 0)
            {
                throw new WebServerException(WebError.InternalServerError, "Authentication not allowed! SessionMode == {0}!", server.SessionMode);
            }
            if (userSession.UserID > 0 && userSession.UserID != user.ID)
            {
                throw new WebServerException(WebError.InternalServerError, "Session belongs to another user!");
            }

            // set session authenticated
            userSession.Expiration = DateTime.UtcNow + server.SessionTimeout;
            userSession.UserID = user.ID;

            // update session at db
            if (server.AuthTables.UserSessions is IMemoryTable)
            {
                server.AuthTables.UserSessions.TryUpdate(userSession);
            }
            else
            {
                Task.Factory.StartNew((s) => { server.AuthTables.UserSessions.TryUpdate((UserSession)s); }, userSession);
            }

            // set globals
            UserSession = userSession;
        }

        /// <summary>Returns a <see cref="string" /> that represents this instance.</summary>
        /// <returns>A <see cref="string" /> that represents this instance.</returns>
        public override string ToString()
        {
            return UserSession.ToString();
        }

        #endregion

        #region public properties

        /// <summary>Gets the authentication user session.</summary>
        /// <value>The authentication user session.</value>
        public UserSession UserSession { get; private set; }

        /// <summary>Gets the user dataset.</summary>
        /// <value>The user dataset.</value>
        public User GetUser()
        {
            if (user.ID != UserSession.ID)
            {
                if (UserSession.IsAuthenticated())
                {
                    User u = server.AuthTables.Users.TryGetStruct(UserSession.UserID);
                    if (u.ID != UserSession.UserID)
                    {
                        throw new Exception("User was deleted!");
                    }

                    user = u;
                }
                else
                {
                    throw new UnauthorizedAccessException("Session is not authenticated!");
                }
            }
            return user;
        }

        /// <summary>Gets the email dataset.</summary>
        /// <value>The email dataset.</value>
        public IList<EmailAddress> GetEmailAddresses()
        {
            if (UserSession.IsAuthenticated())
            {
                return server.AuthTables.EmailAddresses.GetStructs(nameof(EmailAddress.UserID), UserSession.UserID);
            }
            throw new UnauthorizedAccessException("Session is not authenticated!");
        }

        /// <summary>Gets the identifier.</summary>
        /// <value>The identifier.</value>
        public long ID => UserSession.ID;

        /// <summary>Determines whether this instance is authenticated.</summary>
        /// <returns><c>true</c> if this instance is authenticated; otherwise, <c>false</c>.</returns>
        public bool IsAuthenticated()
        {
            return UserSession.IsAuthenticated();
        }
        #endregion
    }
}
