using System;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using Cave.Auth;

namespace Cave.Web
{
    /// <summary>
    /// Provides authentication and user functions on auth.cave.cloud.
    /// </summary>
    public class XAuth
    {
        /// <summary>Initializes a new instance of the <see cref="XAuth"/> class.</summary>
        public XAuth() { }

        #region private class
        System.Threading.Timer timer;

        WebMessage LoadSessionResult(XmlRequest request)
        {
            WebMessage message = request.Result.GetRow<WebMessage>("Result");
            if (message.Error != WebError.None)
            {
                throw new XAuthException(message, request);
            }

            Trace.TraceError("{0}", message);
            User = request.Result.GetRow<User>("User");
            Session = request.Result.GetRow<UserSession>("Session");
            return message;
        }

        void CheckSession(object state)
        {
            try
            {
                CheckSession();
            }
            catch (WebServerException ex)
            {
                switch (ex.Error)
                {
                    case WebError.AuthenticationRequired:
                    case WebError.SessionRequired:
                    {
                        UserSession session = Session;
                        session.Expiration = DateTime.UtcNow.AddSeconds(-1);
                        Session = session;
                    }
                    break;
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("CheckSession error!\n{0}", ex);
            }
            if (Session.IsExpired())
            {
                timer?.Dispose();
                timer = null;
                OnSessionUpdated(new EventArgs());
            }
        }

        /// <summary>Raises the <see cref="E:SessionUpdated" /> event.</summary>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        void OnSessionUpdated(EventArgs e)
        {
            if (SessionUpdated != null)
            {
                Task.Factory.StartNew(() =>
                {
                    SessionUpdated?.Invoke(this, e);
                });
            }
        }
        #endregion

        #region public local functions

        /// <summary>
        /// Loads credentials from the default auth ini file.
        /// </summary>
        /// <param name="user">user name.</param>
        /// <param name="pass">password.</param>
        /// <returns>Returns true on success, false otherwise.</returns>
        public bool LoadCredentials(out string user, out string pass)
        {
            var location = new FileLocation(RootLocation.RoamingUserConfig, "CaveSystems GmbH", null, "auth", Ini.PlatformExtension);
            var reader = IniReader.FromLocation(location);
            user = reader.ReadString("auth", "user");
            pass = reader.ReadString("auth", "pass");
            user = user ?? user.Trim();
            pass = pass ?? pass.Trim();
            return !string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(pass);
        }

        /// <summary>
        /// Saves credentials to the default ini file.
        /// </summary>
        /// <param name="user">user name.</param>
        /// <param name="pass">password.</param>
        public void SaveCredentials(string user, string pass)
        {
            var location = new FileLocation(RootLocation.RoamingUserConfig, "CaveSystems GmbH", null, "auth", Ini.PlatformExtension);
            var writer = IniWriter.FromLocation(location);
            writer.WriteSetting("auth", "user", user);
            writer.WriteSetting("auth", "pass", pass);
            writer.Save();
        }
        #endregion

        #region public remote functions

        /// <summary>Creates a new session.</summary>
        /// <param name="user">The user.</param>
        /// <param name="pass">The pass.</param>
        /// <returns></returns>
        public WebMessage CreateSession(string user, string pass)
        {
            lock (this)
            {
                var request = XmlRequest.Prepare(Server, "CreateSession");
                request.Credentials = new NetworkCredential(user, pass);
                WebMessage message = request.Post();
                if (message.Error != WebError.None)
                {
                    throw new XAuthException(message, request);
                }

                message = LoadSessionResult(request);
                timer?.Dispose();
                timer = new System.Threading.Timer(CheckSession, null, 1000 * 10, 1000 * 10);
                return message;
            }
        }

        /// <summary>Closes the current session.</summary>
        /// <returns></returns>
        public WebMessage CloseSession()
        {
            lock (this)
            {
                if (Session.IsValid())
                {
                    var request = XmlRequest.Prepare(Server, "CloseSession");
                    WebMessage message = request.Post();
                    if (message.Error != WebError.None)
                    {
                        throw new XAuthException(message, request);
                    }

                    return message;
                }
                return WebMessage.Create("CloseSession", "Session was already expired!", error: WebError.None);
            }
        }

        /// <summary>Performs a session check. A successful check extends the session.</summary>
        public void CheckSession()
        {
            Trace.TraceInformation("Checking session {0}", Session);
            var request = XmlRequest.Prepare(Server, "CheckSession");
            request.Headers["Session"] = Session.ID.ToString();
            WebMessage message = request.Post();
            if (message.Error != WebError.None)
            {
                throw new XAuthException(message, request);
            }

            message = LoadSessionResult(request);
            OnSessionUpdated(new EventArgs());
        }

        /// <summary>Creates a new account.</summary>
        /// <param name="user">The user.</param>
        /// <param name="email">The email.</param>
        /// <param name="firstname">The firstname.</param>
        /// <param name="lastname">The lastname.</param>
        /// <param name="birthday">The birthday.</param>
        /// <param name="gender">The gender.</param>
        /// <returns></returns>
        public WebMessage CreateAccount(string user, string email, string firstname, string lastname, DateTime birthday, Gender gender)
        {
            lock (this)
            {
                var request = XmlRequest.Prepare(Server, "CreateAccount", $"user={user}",
                    $"email={email}", $"firstname={firstname}", $"lastname={lastname}", $"gender={gender}",
                    $"birthday={birthday.Date}");
                WebMessage message = request.Post();
                if (message.Error != WebError.None)
                {
                    throw new XAuthException(message, request);
                }

                return message;
            }
        }

        /// <summary>Requests a new password.</summary>
        /// <param name="email">The email.</param>
        /// <returns></returns>
        public WebMessage RequestNewPassword(string email)
        {
            lock (this)
            {
                var request = XmlRequest.Prepare(Server, "RequestNewPassword", $"email={email}");
                WebMessage message = request.Post();
                if (message.Error != WebError.None)
                {
                    throw new XAuthException(message, request);
                }

                return message;
            }
        }

        /// <summary>Gets a transaction key.</summary>
        /// <param name="url">The URL.</param>
        /// <returns></returns>
        public TransactionKey GetTransactionKey(string url)
        {
            lock (this)
            {
                string hash = Base64.UrlChars.Encode(Hash.FromString(Hash.Type.SHA256, url));
                var request = XmlRequest.Prepare(Server, "GetTransactionKey", $"urlHash={hash}");
                request.Headers["Session"] = Session.ID.ToString();
                WebMessage message = request.Post();
                if (message.Error != WebError.None)
                {
                    throw new XAuthException(message, request);
                }

                return request.Result.GetRow<TransactionKey>("TransactionKey");
            }
        }

        /// <summary>Gets the user details.</summary>
        /// <returns></returns>
        public FullUserDetails GetUserDetails()
        {
            lock (this)
            {
                var request = XmlRequest.Prepare(Server, "GetUserDetails");
                request.Headers["Session"] = Session.ID.ToString();
                WebMessage message = request.Post();
                if (message.Error != WebError.None)
                {
                    throw new XAuthException(message, request);
                }

                XmlDeserializer des = request.Result;

                var result = new FullUserDetails
                {
                    User = des.GetRow<User>("User"),
                    UserDetail = des.GetRow<UserDetail>("UserDetail"),
                    Licenses = des.GetTable<License>("Licenses"),
                    Addresses = des.GetTable<Address>("Addresses"),
                    PhoneNumbers = des.GetTable<PhoneNumber>("PhoneNumbers"),
                    EmailAddresses = des.GetTable<EmailAddress>("EmailAddresses"),
                    Groups = des.GetTable<Group>("Groups"),
                    GroupMembers = des.GetTable<GroupMember>("GroupMembers"),
                };
                return result;
            }
        }

        #region Group functions

        /// <summary>Performs a create group command.</summary>
        /// <param name="groupName">Name of the group.</param>
        /// <returns></returns>
        public XmlDeserializer GroupCreate(string groupName)
        {
            lock (this)
            {
                var request = XmlRequest.Prepare(Server, "GroupCreate", $"groupName={groupName}");
                request.Headers["Session"] = Session.ID.ToString();
                WebMessage message = request.Post();
                if (message.Error != WebError.None)
                {
                    throw new XAuthException(message, request);
                }

                return request.Result;
            }
        }

        /// <summary>Performs a invite user to group command.</summary>
        /// <param name="groupID">The group identifier.</param>
        /// <param name="email">The email.</param>
        /// <returns></returns>
        public WebMessage GroupInviteUser(long groupID, string email)
        {
            lock (this)
            {
                var request = XmlRequest.Prepare(Server, "GroupInviteUser", $"groupID={groupID}", $"email={email}");
                request.Headers["Session"] = Session.ID.ToString();
                WebMessage message = request.Post();
                if (message.Error != WebError.None)
                {
                    throw new XAuthException(message, request);
                }

                return message;
            }
        }

        /// <summary>Performs a leave group command.</summary>
        /// <param name="groupID">The group identifier.</param>
        /// <returns></returns>
        public WebMessage GroupLeave(long groupID)
        {
            lock (this)
            {
                var request = XmlRequest.Prepare(Server, "GroupLeave", $"groupID={groupID}");
                request.Headers["Session"] = Session.ID.ToString();
                WebMessage message = request.Post();
                if (message.Error != WebError.None)
                {
                    throw new XAuthException(message, request);
                }

                return message;
            }
        }
        #endregion

        #endregion

        #region public properties

        /// <summary>Gets or sets the server.</summary>
        /// <value>The server.</value>
        public string Server { get; set; } = "https://auth.cave.cloud";

        /// <summary>Occurs when [session updated].</summary>
        public event EventHandler<EventArgs> SessionUpdated;

        /// <summary>Gets the session.</summary>
        /// <value>The session.</value>
        public UserSession Session { get; private set; }

        /// <summary>Gets the user.</summary>
        /// <value>The user.</value>
        public User User { get; private set; }

        /// <summary>Gets the name of the log source.</summary>
        /// <value>The name of the log source.</value>
        public string LogSourceName => "XAuth";
        #endregion
    }
}
