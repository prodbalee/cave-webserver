using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using Cave.Auth;
using Cave.Data;
using Cave.IO;
using Cave.Media;
using Cave.Net;
using Cave.Net.Dns;

namespace Cave.Web.Auth
{
    /// <summary>
    /// Provides an authentication interface for <see cref="WebServer"/>
    /// </summary>
    /// <typeparam name="T">The UserLevel enum (int)</typeparam>
    public class AuthInterface<T> where T : IConvertible
    {
        AuthMailSender authMailSender;

        void CheckAuth(object sender, WebServerAuthEventArgs e)
        {
            if (DefaultLocalhostUser.ID > 0)
            {
                if (IPAddress.TryParse(e.Data.Request.SourceAddress, out IPAddress address) && NetTools.IsLocalhost(address))
                {
                    e.SetAuthentication(DefaultLocalhostUser, UserSessionFlags.IsLocalhost);
                    return;
                }
                switch (e.Data.Request.SourceAddress)
                {
                    case "localhost":
                    {
                        e.SetAuthentication(DefaultLocalhostUser, UserSessionFlags.IsLocalhost);
                        return;
                    }
                }
            }
            switch (e.AuthType)
            {
                case WebServerAuthType.None: return;
                case WebServerAuthType.Basic:
                {
                    if (true == e.Data.Server.AuthTables.Login(e.Username, e.Password, out User user, out EmailAddress email))
                    {
                        e.SetAuthentication(user, 0);
                    }
                    e.Data.Server.AuthTables.Save();
                    return;
                }
                default:
                case WebServerAuthType.Session:
                {
                    throw new InvalidOperationException();
                }
            }
        }

        /// <summary>Initializes a new instance of the <see cref="AuthInterface{T}" /> class.</summary>
        /// <param name="server">The server.</param>
        public AuthInterface(WebServer server)
        {
            server.CheckSession += this.CheckAuth;
            if (server.AuthTables == null)
            {
                throw new WebServerException(WebError.InternalServerError, "Server.AuthTables need to be set!");
            }
        }

        /// <summary>Sets the force email verification flag and loads the mail server settings from the specified settings.</summary>
        /// <param name="settings">The ini.</param>
        public void EnableEmailVerification(ISettings settings)
        {
            authMailSender = new AuthMailSender(settings);
        }

        /// <summary>Gets a value indicating whether [email verification] is used.</summary>
        /// <value><c>true</c> if [email verification] is used; otherwise, <c>false</c>.</value>
        /// <remarks>Use <see cref="EnableEmailVerification(ISettings)"/> to enable this feature.</remarks>
        public bool RequireEmailVerification => authMailSender != null;

        /// <summary>Gets or sets a value indicating whether require valid first and lastnames during registration at <see cref="CreateAccount(WebData, string, string, string, string, DateTime?, Gender, string)"/>.</summary>
        /// <value><c>true</c> if [require valid first and lastnames]; otherwise, <c>false</c>.</value>
        public bool RequireValidNames { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether [require unique nicknames].
        /// </summary>
        /// <value><c>true</c> if [require unique nicknames]; otherwise, <c>false</c>.</value>
        public bool RequireUniqueNicknames { get; set; }

        /// <summary>Gets or sets the default localhost user.</summary>
        /// <value>The default localhost user.</value>
        public User DefaultLocalhostUser { get; set; }

        /// <summary>Retrieves the users session data.</summary>
        /// <param name="data">The web data.</param>
        /// <remarks>Returns <see cref="UserSession"/> and if the session is authorized <see cref="User"/>, <see cref="EmailAddress"/></remarks>
        [WebPage(Paths = "/auth/session")]
        public void GetSession(WebData data)
        {
            data.Result.AddMessage(data.Method, "User session retrieved.");
            data.Result.AddStruct(data.Session.UserSession);
            if (data.Session.IsAuthenticated())
            {
                data.Result.AddStruct(data.Session.GetUser().ClearPrivateFields());
                data.Result.AddStructs(data.Session.GetEmailAddresses());
            }
        }

        #region /auth/admin/...
        /// <summary>Lists all users.</summary>
        /// <param name="data">The web data.</param>
        /// <remarks>Returns <see cref="User"/>, <see cref="EmailAddress"/></remarks>
        [WebPage(Paths = "/auth/admin/list/users", AuthType = WebServerAuthType.Session, AuthData = "Admin")]
        public void GetUserList(WebData data)
        {
            data.Result.AddMessage(data.Method, "Userlist retrieved.");
            var users = data.Server.AuthTables.Users.GetStructs();
            data.Result.AddStructs(users.Select(u => u.ClearPrivateFields()));
            data.Result.AddTable(data.Server.AuthTables.EmailAddresses);
        }

        /// <summary>Deletes the user with the specified identifier.</summary>
        /// <param name="data">The data.</param>
        /// <param name="userID">The user identifier.</param>
        /// <exception cref="WebServerException">User does not exist!</exception>
        /// <remarks>Returns <see cref="User"/>, <see cref="EmailAddress"/></remarks>
        [WebPage(Paths = "/auth/admin/user/delete", AuthType = WebServerAuthType.Session, AuthData = "Admin")]
        public void DeleteUser(WebData data, long userID)
        {
            AuthTables authTables = data.Server.AuthTables;
            if (!authTables.Users.TryDelete(userID))
            {
                throw new WebServerException(WebError.InvalidParameters, "User does not exist!");
            }
            authTables.Addresses.TryDelete(nameof(Address.UserID), userID);
            authTables.EmailAddresses.TryDelete(nameof(EmailAddress.UserID), userID);
            authTables.GroupMembers.TryDelete(nameof(GroupMember.UserID), userID);
            authTables.Licenses.TryDelete(nameof(License.UserID), userID);
            authTables.PhoneNumbers.TryDelete(nameof(PhoneNumber.UserID), userID);
            authTables.UserConfigurations.TryDelete(nameof(UserConfiguration.UserID), userID);
            authTables.UserDetails.TryDelete(nameof(UserDetail.UserID), userID);
            authTables.UserSessions.TryDelete(nameof(UserSession.UserID), userID);
            data.Result.AddMessage(data.Method, "User deleted!");
            authTables.Save();
            GetUserList(data);
        }

        /// <summary>Modifies the user with the specified identifier.</summary>
        /// <param name="data">The data.</param>
        /// <param name="userID">The user identifier.</param>
        /// <param name="nickName">Name of the nick.</param>
        /// <param name="userLevel">The new user level.</param>
        /// <param name="invalidateOTP">A flag telling whether to invalidate the one time password id of the user. This cannot be used without setting a new password!</param>
        /// <param name="password">A new password for the user.</param>
        /// <exception cref="WebServerException">User does not exist!
        /// or
        /// NickName is already registered.</exception>
        /// <remarks>Returns <see cref="User" />, <see cref="EmailAddress" /></remarks>
        [WebPage(Paths = "/auth/admin/user/modify", AuthType = WebServerAuthType.Session, AuthData = "Admin")]
        public void ModifyUser(WebData data, long userID, string nickName = null, int? userLevel = null, bool? invalidateOTP = null, string password = null)
        {
            AuthTables authTables = data.Server.AuthTables;
            lock (authTables.Users)
            {
                User user = authTables.Users.TryGetStruct(userID);
                if (user.ID <= 0)
                {
                    throw new WebServerException(WebError.InvalidParameters, "User does not exist!");
                }
                if (nickName != null)
                {
                    user.NickName = nickName;
                    if (RequireUniqueNicknames && authTables.Users.Exist(nameof(User.NickName), nickName))
                    {
                        throw new WebServerException(WebError.InvalidParameters, "NickName is already registered.");
                    }
                }
                if (userLevel.HasValue)
                {
                    user.AuthLevel = userLevel.Value;
                }

                if (password != null)
                {
                    if (true == invalidateOTP)
                    {
                        user.SetRandomSalt();
                    }

                    user.SetPassword(password);
                }
                data.Server.AuthTables.Users.Replace(user);
            }
            data.Result.AddMessage(data.Method, "User modified!");
            authTables.Save();
            GetUserList(data);
        }

        /// <summary>Gets the user details. This requires an authenticated session.</summary>
        /// <param name="data">The data.</param>
        /// <param name="userID">The user identifier.</param>
        /// <exception cref="WebServerException"><see cref="WebError.SessionRequired" /> Session Object {0} is not available at the session!</exception>
        /// <remarks>
        /// Returns <see cref="WebMessage" /> (Result), <see cref="User" /> (User),
        /// <see cref="License" /> (Licenses), <see cref="Address" /> (Addresses), <see cref="PhoneNumber" /> (PhoneNumbers),
        /// <see cref="EmailAddress" /> (EmailAddresses), <see cref="Group" /> (Groups: the user owns or may access)
        /// <see cref="GroupMember" /> (GroupMembers), <see cref="UserSessionLicense" /> (ActiveSessions), <see cref="User" /> (ActiveUsers of shared licenses)
        /// </remarks>
        [WebPage(Paths = "/auth/admin/account/details", AuthType = WebServerAuthType.Session, AuthData = "Admin")]
        public void GetAccountDetailsByUser(WebData data, long userID)
        {
            AuthTables authTables = data.Server.AuthTables;
            User user = authTables.Users.GetStruct(userID);
            authTables.UserDetails.TryGetStruct(user.ID, out UserDetail userDetail); userDetail.UserID = user.ID;
            var groupMember = authTables.GroupMembers.GetStructs(nameof(GroupMember.UserID), user.ID);
            var groups = authTables.Groups.GetStructs(groupMember.Select(m => m.GroupID).ToArray());

            //get licenses and transfer without certificate data
            List<License> licenses = new List<License>();
            licenses.AddRange(authTables.GetGroupLicenses(groups.Select(g => g.ID)));
            licenses.AddRange(authTables.GetUserLicenses(user.ID));
            for (int i = 0; i < licenses.Count; i++)
            {
                License l = licenses[i];
                l.Certificate = null;
                licenses[i] = l;
            }

            var phoneNumbers = authTables.PhoneNumbers.GetStructs(nameof(PhoneNumber.UserID), user.ID);
            var emailAddresses = authTables.EmailAddresses.GetStructs(nameof(PhoneNumber.UserID), user.ID);
            var addresses = authTables.Addresses.GetStructs(nameof(PhoneNumber.UserID), user.ID);
            var userSessionLicenses = authTables.UserSessionLicenses.GetStructs(Search.FieldIn(nameof(UserSessionLicense.LicenseID), licenses.Select(l => l.ID)));
            var activeUsers = authTables.Users.GetStructs(userSessionLicenses.Select(usl => usl.UserID).Distinct());

            user.ClearPrivateFields();
            data.Result.AddMessage(data.Method, "User details retrieved");
            data.Result.AddStruct(user);
            data.Result.AddStruct(userDetail);
            data.Result.AddStructs(licenses);
            data.Result.AddStructs(addresses);
            data.Result.AddStructs(phoneNumbers);
            data.Result.AddStructs(emailAddresses);
            data.Result.AddStructs(groups);
            data.Result.AddStructs(groupMember);
            data.Result.AddStructs(userSessionLicenses, tableName: "ActiveSessions");
            data.Result.AddStructs(activeUsers, tableName: "ActiveUsers");
        }

        #endregion

        #region /auth/list/...

        /// <summary>Lists all user levels.</summary>
        /// <param name="data">The data.</param>
        /// <remarks>Returns <see cref="UserLevel"/></remarks>
        [WebPage(Paths = "/auth/list/levels")]
        public void GetLevelList(WebData data)
        {
            data.Result.AddMessage(data.Method, "Userlevels retrieved.");
            data.Result.AddStructs(UserLevel.FromEnum(typeof(T)));
        }

        /// <summary>
        /// Retrieves all available country datasets
        /// </summary>
        /// <param name="data">The data.</param>
        /// <remarks>Returns <see cref="Country"/>.</remarks>
        [WebPage(Paths = "/auth/list/countries")]
        public void GetCountryList(WebData data)
        {
            data.Result.AddMessage(data.Method, "Countries retrieved");
            data.Result.AddTable(data.Server.AuthTables.Countries);
        }

        #endregion

        #region /auth/account

        #region /auth/account/...

        /// <summary>Creates a new user account.</summary>
        /// <param name="data">The data.</param>
        /// <param name="nickname">The username.</param>
        /// <param name="email">The email address.</param>
        /// <param name="firstname">The firstname.</param>
        /// <param name="lastname">The lastname.</param>
        /// <param name="birthday">The birthday.</param>
        /// <param name="gender">The gender.</param>
        /// <param name="password">The password. If none is specified a default password is generated.</param>
        /// <exception cref="WebServerException"><see cref="WebError.InvalidParameters" /> Birthday not in valid range!
        /// or
        /// <see cref="WebError.InvalidParameters" /> Usernames with less than 8 characters are not available to the public!
        /// or
        /// <see cref="WebError.InvalidParameters" /> Missing firstname/lastname!
        /// or
        /// <see cref="WebError.InvalidParameters" /> Invalid email address!</exception>
        /// <remarks>Returns <see cref="WebMessage" />, <see cref="User"/>, <see cref="EmailAddress"/>, <see cref="UserDetail"/>, <see cref="NewPasswordNotification"/></remarks>
        [WebPage(Paths = "/auth/account/create")]
        public void CreateAccount(WebData data, string nickname, string email = null, string firstname = null, string lastname = null, DateTime? birthday = null, Gender gender = 0, string password = null)
        {
            AuthTables authTables = data.Server.AuthTables; User user; EmailAddress emailAddress;
            if (nickname == null)
            {
                throw new ArgumentNullException(nameof(nickname));
            }

            if (birthday.HasValue && birthday < DateTime.UtcNow - TimeSpan.FromDays(365 * 120) || birthday > DateTime.UtcNow - TimeSpan.FromDays(12 * 365))
            {
                throw new WebServerException(WebError.InvalidParameters, "Birthday not in valid range!");
            }
            if (RequireValidNames)
            {
                if (nickname.Length < 8)
                {
                    string host = new MailAddress(email).Host;
                    switch (host)
                    {
                        case "cavemail.de": //TODO use collection to check for domains
                        case "cavemail.org":
                        case "cavesystems.de":
                        case "cave.cloud": break;
                        default: throw new WebServerException(WebError.InvalidParameters, 0, "Usernames with less than 8 characters are not available to the public!");
                    }
                }
                if (string.IsNullOrEmpty(firstname) || string.IsNullOrEmpty(lastname))
                {
                    throw new WebServerException(WebError.InvalidParameters, 0, "Missing firstname/lastname!");
                }

                ConnectionString cs = email;
                DnsResponse resp = DnsClient.Default.Resolve(cs.Server, DnsRecordType.MX);
                if (resp.Answers.Count == 0)
                {
                    throw new WebServerException(WebError.InvalidParameters, 0, "Invalid email address!");
                }
            }

            lock (authTables.Users)
            {
                if (RequireUniqueNicknames)
                {
                    if (authTables.Users.Exist(nameof(User.NickName), nickname))
                    {
                        throw new WebServerException(WebError.InvalidParameters, "Nickname is already in use!");
                    }
                }

                if (RequireEmailVerification)
                {
                    authTables.CreateUser(nickname, email, out emailAddress, out user);
                    if (!authMailSender.SendAuthMessage(user, emailAddress))
                    {
                        authTables.Users.Delete(user.ID);
                        authTables.EmailAddresses.Delete(emailAddress.ID);
                        throw new WebServerException(WebError.InvalidParameters, 0, "Invalid email address!");
                    }

                    data.Result.AddMessage(data.Method, $"An email was sent to {email}. Please check your inbox and approve your email address by clicking the confirmation link within the email.");
                }
                else
                {
                    if (string.IsNullOrEmpty(password))
                    {
                        password = DefaultRNG.GetPassword(12);
                    }

                    authTables.CreateUser(nickname, email, password, UserState.Confirmed, 0, out user, out emailAddress);
                    data.Result.AddMessage(data.Method, $"User account created.");
                    string otpSecret = Base32.OTP.Encode(user.Salt.GetRange(10));
                    data.Result.AddStruct(new NewPasswordNotification()
                    {
                        ID = user.ID,
                        Password = password,
                        OTPSecret = otpSecret,
                        OTPLink = TimeBasedOTP.GetGoogleQRLink(otpSecret),
                    });
                }
                UserDetail userDetails = new UserDetail()
                {
                    Birthday = birthday.GetValueOrDefault(),
                    Gender = gender,
                    FirstName = firstname,
                    LastName = lastname,
                    PhonePIN = (int)(DefaultRNG.UInt32 % 100000),
                    UserID = user.ID
                };
                authTables.UserDetails.Replace(userDetails);
                data.Result.AddStruct(user.ClearPrivateFields());
                data.Result.AddStruct(emailAddress);
                data.Result.AddStruct(userDetails);
                authTables.Save();
            }
        }

        /// <summary>Verifies a code sent to an email address.</summary>
        /// <param name="data">The data.</param>
        /// <param name="email">The email address.</param>
        /// <param name="code">The code.</param>
        /// <exception cref="WebServerException">
        /// <see cref="WebError.InvalidParameters"/> Invalid email address!
        /// or
        /// <see cref="WebError.InvalidParameters"/> Verification error for email {0}!
        /// </exception>
        /// <remarks>Returns <see cref="WebMessage"/>, <see cref="NewPasswordNotification"/></remarks>
        [WebPage(Paths = "/auth/account/verify")]
        public void VerifyAccount(WebData data, string email, string code)
        {
            AuthTables authTables = data.Server.AuthTables;
            var addresses = authTables.EmailAddresses.GetStructs(nameof(EmailAddress.Address), email);
            if (addresses.Count > 1)
            {
                throw new WebServerException(WebError.InvalidParameters, 0, string.Format("Email address {0} not unique, please contact support!", email));
            }
            if (addresses.Count == 1)
            {
                EmailAddress e = addresses[0];
                User u = authTables.Users[e.UserID];
                if ((e.VerificationCode == code) && (u.State == UserState.New || u.State == UserState.PasswordResetRequested))
                {
                    if (e.VerificationCode == code)
                    {
                        e.Verified = true;
                    }

                    e.VerificationCode = null;
                    string pass = DefaultRNG.GetPassword(12);
                    u.SetPassword(pass);
                    u.State = UserState.Confirmed;
                    authTables.Update(ref u, ref e);

                    data.Result.AddMessage(data.Method, $"User account {u.NickName} was verified with email address {e.Address}!");
                    string otpSecret = Base32.OTP.Encode(u.Salt.GetRange(10));
                    data.Result.AddStruct(new NewPasswordNotification()
                    {
                        ID = u.ID,
                        Password = pass,
                        OTPSecret = otpSecret,
                        OTPLink = TimeBasedOTP.GetGoogleQRLink(otpSecret),
                    });
                    authTables.Save();
                    return;
                }
            }
            throw new WebServerException(WebError.InvalidParameters, 0, string.Format("Verification error for email {0}!", email));
        }

        /// <summary>Requests a new password for an existing user.</summary>
        /// <remarks>Returns <see cref="WebMessage"/> (Result)</remarks>
        /// <param name="data">The data.</param>
        /// <param name="email">The email.</param>
        /// <exception cref="WebServerException">
        /// <see cref="WebError.InvalidParameters"/> We could not send an email to {email}.
        /// </exception>
        /// <remarks>Returns <see cref="WebMessage"/></remarks>
        [WebPage(Paths = "/auth/account/request-new-password")]
        public void RequestNewPassword(WebData data, string email)
        {
            AuthTables authTables = data.Server.AuthTables; User u; EmailAddress e;
            try
            {
                authTables.RequestPasswordReset(email, out e, out u);
            }
            catch
            {
                throw new WebServerException(WebError.InvalidParameters, $"We could not send an email to {email}.");
            }
            if (!authMailSender.SendAuthMessage(u, e))
            {
                throw new WebServerException(WebError.InvalidParameters,
                    $"We could not send an email to {email}. Please contact support or use another email address.");
            }
            data.Result.AddMessage(data.Method, $"An email was sent to {email}. Please check your inbox and approve your email address by clicking the confirmation link within the email.");
            authTables.Save();
        }

        /// <summary>Closes a user session.</summary>
        /// <param name="data">The data.</param>
        /// <exception cref="WebServerException">
        /// <see cref="WebError.SessionRequired" /> Session Object {0} is not available at the session!
        /// </exception>
        /// <remarks>Returns <see cref="WebMessage"/> (Result)</remarks>
        [WebPage(Paths = "/auth/account/logout")]
        public void Logout(WebData data)
        {
            data.Result.AddMessage(data.Method, "User no longer authenticated. Session closed.");
            data.Session.Expire();
        }

        /// <summary>Openes a user session.</summary>
        /// <param name="data">The data.</param>
        /// <param name="redirect">The redirection target on success</param>
        /// <exception cref="WebServerException">
        /// <see cref="WebError.SessionRequired" /> Session Object {0} is not available at the session!
        /// </exception>
        /// <remarks>Returns <see cref="WebMessage"/>, <see cref="User"/>, <see cref="EmailAddress"/></remarks>
        [WebPage(Paths = "/auth/account/login", AuthType = WebServerAuthType.Basic)]
        public void Login(WebData data, string redirect = null)
        {
            if (redirect != null)
            {
                data.Result.AddMessage(data.Method, WebError.Redirect, $"Image not available!\nPlease use <a href=\"{redirect}\">this link</a>.");
                data.Result.Headers["Location"] = redirect;
                data.Result.Type = WebResultType.Html;
                return;
            }
            data.Result.AddMessage(data.Method, "User authenticated. Session {0} valid.", data.Session.ID);
            GetSession(data);
        }

        /// <summary>Sets the nickname for the account.</summary>
        /// <param name="data">The data.</param>
        /// <param name="nickName">New nickname.</param>
        /// <remarks>Returns <see cref="WebMessage"/>, <see cref="User"/></remarks>
        [WebPage(Paths = "/auth/account/nickname/update", AuthType = WebServerAuthType.Session)]
        public void UpdateNickname(WebData data, string nickName)
        {
            AuthTables authTables = data.Server.AuthTables;
            User user = data.Session.GetUser();
            lock (authTables.Users)
            {
                if (RequireUniqueNicknames)
                {
                    if (authTables.Users.Exist(nameof(User.NickName), nickName))
                    {
                        throw new WebServerException(WebError.InvalidParameters, "Nickname is already in use!");
                    }
                }
                user.NickName = nickName;
                authTables.Users.TryUpdate(user);
                authTables.Save();
            }
            data.Result.AddMessage(data.Method, "User nickname updated.");
            data.Result.AddStruct(user.ClearPrivateFields());
        }

        /// <summary>Sets the nickname for the account.</summary>
        /// <param name="data">The data.</param>
        /// <param name="oldPassword">The old password.</param>
        /// <param name="newPassword">The new password.</param>
        /// <remarks>Returns <see cref="WebMessage" />, <see cref="User" /></remarks>
        [WebPage(Paths = "/auth/account/password/update", AuthType = WebServerAuthType.Session)]
        public void UpdatePassword(WebData data, string oldPassword, string newPassword)
        {
            AuthTables authTables = data.Server.AuthTables;
            User user = data.Session.GetUser();
            user.ChangePassword(oldPassword, newPassword);
            authTables.Users.TryUpdate(user);
            authTables.Save();
            data.Result.AddMessage(data.Method, "User password updated.");
            data.Result.AddStruct(user.ClearPrivateFields());
        }

        /// <summary>Creates a new avatar for the user.</summary>
        /// <param name="data">The data.</param>
        /// <param name="avatarID">The avatar identifier.</param>
        /// <remarks>Returns <see cref="WebMessage" />, <see cref="User" /></remarks>
        [WebPage(Paths = "/auth/account/avatar/new", AuthType = WebServerAuthType.Session)]
        public void NewAvatar(WebData data, long avatarID = 0)
        {
            AuthTables authTables = data.Server.AuthTables;
            User user = data.Session.GetUser();
            if (avatarID > 0)
            {
                user.AvatarID = avatarID;
                authTables.Users.Replace(user);
                authTables.Save();
                data.Result.AddMessage(data.Method, "{0} Avatar updated.", user);
                data.Result.AddStruct(user.ClearPrivateFields());
                return;
            }
            int i = 0;
            while (i++ < 20)
            {
                user.AvatarID = DefaultRNG.UInt32;
                if (user.AvatarID > 0 && !authTables.Users.Exist(nameof(User.AvatarID), user.AvatarID))
                {
                    authTables.Users.Replace(user);
                    authTables.Save();
                    data.Result.AddMessage(data.Method, "{0} Avatar updated.", user);
                    data.Result.AddStruct(user.ClearPrivateFields());
                    return;
                }
            }
            data.Result.AddMessage(data.Method, WebError.InternalServerError, "Could not update avatar of user {0}", user);
        }

        /// <summary>Creates a new color for the user.</summary>
        /// <param name="data">The data.</param>
        /// <remarks>Returns <see cref="WebMessage"/>, <see cref="User"/></remarks>
        [WebPage(Paths = "/auth/account/color/new", AuthType = WebServerAuthType.Session)]
        public void NewColor(WebData data)
        {
            AuthTables authTables = data.Server.AuthTables;
            User user = data.Session.GetUser();
            int i = 0;
            while (i++ < 20)
            {
                user.Color = ARGB.Random.AsUInt32;
                if (user.AvatarID > 0 && !authTables.Users.Exist(nameof(User.Color), user.Color))
                {
                    authTables.Users.Replace(user);
                    authTables.Save();
                    data.Result.AddMessage(data.Method, "{0} Color updated.", user);
                    data.Result.AddStruct(user.ClearPrivateFields());
                    return;
                }
            }
            data.Result.AddMessage(data.Method, WebError.InternalServerError, "Could not update color of user {0}", user);
        }
        #endregion

        #region /auth/account/config/...
        /// <summary>Sets/gets a configurations for the current user and specified progam identifier.
        /// DELETE removes, PUT and POST replaces and GET retrieves the current configuration.</summary>
        /// <param name="data">The data.</param>
        /// <param name="programID">The program identifier.</param>
        /// <exception cref="WebServerException">
        /// <see cref="WebError.InvalidParameters" />ConfigurationSet requires a post request!
        /// or
        /// <see cref="WebError.SessionRequired" /> Session Object {0} is not available at the session!
        /// </exception>
        /// <remarks>Returns <see cref="WebMessage"/>, <see cref="UserConfiguration"/></remarks>
        [WebPage(Paths = "/auth/account/config", AuthType = WebServerAuthType.Session)]
        public void AccountConfiguration(WebData data, long programID)
        {
            switch (data.Request.Command)
            {
                case WebCommand.PUT:
                case WebCommand.POST:
                case WebCommand.DELETE:
                    SetAccountConfiguration(data, programID);
                    return;
                case WebCommand.GET:
                    GetAccountConfiguration(data, programID);
                    return;
            }
        }

        /// <summary>Sets the user configuration for the specified program.</summary>
        /// <param name="data">The data.</param>
        /// <param name="programID">The program identifier.</param>
        /// <exception cref="WebServerException">
        /// <see cref="WebError.SessionRequired" /> Session Object {0} is not available at the session!
        /// </exception>
        /// <remarks>Returns <see cref="WebMessage"/>, <see cref="UserConfiguration"/>.</remarks>
        [WebPage(Paths = "/auth/account/config/set", AuthType = WebServerAuthType.Session)]
        public void SetAccountConfiguration(WebData data, long programID)
        {
            AuthTables authTables = data.Server.AuthTables;
            User user = data.Session.GetUser();
            if (data.Request.PostData == null)
            {
                throw new WebServerException(WebError.InvalidParameters, 0, "ConfigurationSet requires a post request!");
            }
            var existing = authTables.UserConfigurations.FindRows(
                Search.FieldEquals(nameof(UserConfiguration.UserID), user.ID) &
                Search.FieldEquals(nameof(UserConfiguration.ProgramID), programID));

            if (data.Request.PostData == null)
            {
                authTables.UserConfigurations.Delete(existing);
                data.Result.AddMessage(data.Method, string.Format("Deleted {0} configurations", existing.Count));
            }
            else
            {
                UserConfiguration config = new UserConfiguration()
                {
                    ID = existing.FirstOrDefault(),
                    ProgramID = programID,
                    UserID = user.ID,
                    Data = data.Request.PostData,
                };
                authTables.UserConfigurations.Replace(config);
                authTables.Save();
                data.Result.AddMessage(data.Method, "User configuration replaced");
            }
        }

        /// <summary>Gets the user configuration for the specified program.</summary>
        /// <param name="data">The data.</param>
        /// <param name="programID">The program identifier.</param>
        /// <exception cref="WebServerException">
        /// <see cref="WebError.SessionRequired" /> Session Object {0} is not available at the session!
        /// </exception>
        /// <remarks>Returns <see cref="WebMessage"/>, <see cref="UserConfiguration"/>.</remarks>
        [WebPage(Paths = "/auth/account/config/get", AuthType = WebServerAuthType.Session)]
        public void GetAccountConfiguration(WebData data, long programID)
        {
            AuthTables authTables = data.Server.AuthTables;
            User user = data.Session.GetUser();
            authTables.UserConfigurations.TryGetStruct(
                Search.FieldEquals(nameof(UserConfiguration.UserID), user.ID) &
                Search.FieldEquals(nameof(UserConfiguration.ProgramID), programID),
                out UserConfiguration config);

            switch (data.Request.Extension)
            {
                case ".htm":
                case ".html":
                case ".xml":
                case ".json":
                case ".csv":
                case ".txt":
                    data.Result.AddMessage(data.Method, "User configuration retrieved");
                    data.Result.AddStruct(user.ClearPrivateFields());
                    data.Result.AddStruct(config);
                    break;
                default:
                    data.Answer = WebAnswer.Raw(data.Request, WebMessage.Create(data.Method, "File retrieved"), config.Data, "application/octet-stream");
                    break;
            }
        }

        #endregion

        #region /auth/account/details/...

        /// <summary>Gets the user details. This requires an authenticated session.</summary>
        /// <param name="data">The data.</param>
        /// <exception cref="WebServerException"><see cref="WebError.SessionRequired" /> Session Object {0} is not available at the session!</exception>
        /// <remarks>Returns <see cref="WebMessage"/> (Result), <see cref="User"/> (User),
        /// <see cref="License"/> (Licenses), <see cref="Address"/> (Addresses), <see cref="PhoneNumber"/> (PhoneNumbers),
        /// <see cref="EmailAddress"/> (EmailAddresses), <see cref="Group"/> (Groups: the user owns or may access)
        /// <see cref="GroupMember"/> (GroupMembers), <see cref="UserSessionLicense"/> (ActiveSessions), <see cref="User"/> (ActiveUsers of shared licenses)
        /// </remarks>
        [WebPage(Paths = "/auth/account/details", AuthType = WebServerAuthType.Session)]
        public void GetAccountDetails(WebData data)
        {
            AuthTables authTables = data.Server.AuthTables;
            User user = data.Session.GetUser();
            authTables.UserDetails.TryGetStruct(user.ID, out UserDetail userDetail); userDetail.UserID = user.ID;
            var groupMember = authTables.GroupMembers.GetStructs(nameof(GroupMember.UserID), user.ID);
            var groups = authTables.Groups.GetStructs(groupMember.Select(m => m.GroupID).ToArray());

            //get licenses and transfer without certificate data
            var licenses = new List<License>();
            licenses.AddRange(authTables.GetGroupLicenses(groups.Select(g => g.ID)));
            licenses.AddRange(authTables.GetUserLicenses(user.ID));
            for (int i = 0; i < licenses.Count; i++)
            {
                License l = licenses[i];
                l.Certificate = null;
                licenses[i] = l;
            }

            var phoneNumbers = authTables.PhoneNumbers.GetStructs(nameof(PhoneNumber.UserID), user.ID);
            var emailAddresses = authTables.EmailAddresses.GetStructs(nameof(PhoneNumber.UserID), user.ID);
            var addresses = authTables.Addresses.GetStructs(nameof(PhoneNumber.UserID), user.ID);
            var userSessionLicenses = authTables.UserSessionLicenses.GetStructs(Search.FieldIn(nameof(UserSessionLicense.LicenseID), licenses.Select(l => l.ID)));
            var activeUsers = authTables.Users.GetStructs(userSessionLicenses.Select(usl => usl.UserID).Distinct());

            data.Result.AddMessage(data.Method, "User details retrieved");
            data.Result.AddStruct(user.ClearPrivateFields());
            data.Result.AddStruct(userDetail);
            data.Result.AddStructs(licenses);
            data.Result.AddStructs(addresses);
            data.Result.AddStructs(phoneNumbers);
            data.Result.AddStructs(emailAddresses);
            data.Result.AddStructs(groups);
            data.Result.AddStructs(groupMember);
            data.Result.AddStructs(userSessionLicenses, tableName: "ActiveSessions");
            data.Result.AddStructs(activeUsers, tableName: "ActiveUsers");
        }

        #endregion

        #region /auth/account/address/...
        /// <summary>Set the primary address of an account.</summary>
        /// <param name="data">The data.</param>
        /// <param name="addressID">The address identifier.</param>
        /// <exception cref="Cave.Web.WebServerException">Address does not exist or does not belong to the current user!</exception>
        /// <remarks>Returns <see cref="WebMessage"/>, <see cref="User"/>, <see cref="Address"/></remarks>
        [WebPage(Paths = "/auth/account/address/setprimary", AuthType = WebServerAuthType.Session)]
        public void SetPrimaryAddress(WebData data, long addressID)
        {
            AuthTables authTables = data.Server.AuthTables;
            User user = data.Session.GetUser();
            if (!authTables.Addresses.TryGetStruct(addressID, out Address address) || address.UserID != user.ID)
            {
                throw new WebServerException(WebError.InvalidParameters, "Address does not exist or does not belong to the current user!");
            }
            foreach (Address p in authTables.Addresses.GetStructs(nameof(PhoneNumber.UserID), user.ID))
            {
                if (!p.IsPrimary)
                {
                    continue;
                }

                Address update = p;
                update.IsPrimary = false;
                authTables.Addresses.Update(update);
            }
            address.IsPrimary = true;
            authTables.Addresses.Update(address);
            authTables.Save();
            data.Result.AddMessage(data.Method, "User primary address set.");
            data.Result.AddStruct(user.ClearPrivateFields());
            data.Result.AddStructs(authTables.Addresses.GetStructs(nameof(Address.UserID), user.ID));
        }

        /// <summary>Adds a new address.</summary>
        /// <param name="data">The data.</param>
        /// <param name="countryID">The country identifier.</param>
        /// <param name="text">The text.</param>
        /// <remarks>Returns <see cref="WebMessage"/>, <see cref="User"/>, <see cref="Address"/></remarks>
        [WebPage(Paths = "/auth/account/address/add", AuthType = WebServerAuthType.Session)]
        public void AddAddress(WebData data, long countryID, string text)
        {
            AuthTables authTables = data.Server.AuthTables;
            User user = data.Session.GetUser();
            Address address = new Address()
            {
                CountryID = countryID,
                UserID = user.ID,
                Text = text,
                IsPrimary = authTables.Addresses.Count(nameof(Address.UserID), user.ID) == 0,
            };
            address.ID = authTables.Addresses.Insert(address);
            authTables.Save();
            data.Result.AddMessage(data.Method, "User address added");
            data.Result.AddStruct(user.ClearPrivateFields());
            data.Result.AddStructs(authTables.Addresses.GetStructs(nameof(Address.UserID), user.ID));
        }

        /// <summary>Removes an address.</summary>
        /// <param name="data">The data.</param>
        /// <param name="addressID">The address identifier.</param>
        /// <exception cref="Cave.Web.WebServerException">
        /// Address does not exist or does not belong to the current user!
        /// or
        /// Cannot remove primary address!
        /// </exception>
        /// <remarks>Returns <see cref="WebMessage"/>, <see cref="User"/>, <see cref="Address"/> (DeletedAddresses)</remarks>
        [WebPage(Paths = "/auth/account/address/delete", AuthType = WebServerAuthType.Session)]
        public void DeleteAddress(WebData data, long addressID)
        {
            AuthTables authTables = data.Server.AuthTables;
            User user = data.Session.GetUser();
            if (!authTables.Addresses.TryGetStruct(addressID, out Address address) || address.UserID != user.ID)
            {
                throw new WebServerException(WebError.InvalidParameters, "Address does not exist or does not belong to the current user!");
            }
            if (address.IsPrimary)
            {
                throw new WebServerException(WebError.InvalidOperation, "Cannot remove primary address!");
            }

            authTables.Addresses.Delete(address.ID);
            authTables.Save();
            data.Result.AddMessage(data.Method, "User address deleted");
            data.Result.AddStruct(user.ClearPrivateFields());
            data.Result.AddStructs(authTables.Addresses.GetStructs(nameof(Address.UserID), user.ID));
        }
        #endregion

        #region /auth/account/phone/...

        /// <summary>Sets a primary phone number.</summary>
        /// <param name="data">The data.</param>
        /// <param name="phoneNumberID">The phone number identifier.</param>
        /// <exception cref="Cave.Web.WebServerException">
        /// PhoneNumber does not exist or does not belong to the current user!
        /// or
        /// Cannot set primary phone number without verification!
        /// </exception>
        /// <remarks>Returns <see cref="WebMessage"/>, <see cref="User"/>, <see cref="PhoneNumber"/></remarks>
        [WebPage(Paths = "/auth/account/phonenumber/setprimary", AuthType = WebServerAuthType.Session)]
        public void SetPrimaryPhoneNumber(WebData data, long phoneNumberID)
        {
            AuthTables authTables = data.Server.AuthTables;
            User user = data.Session.GetUser();
            if (!authTables.PhoneNumbers.TryGetStruct(phoneNumberID, out PhoneNumber phone) || phone.UserID != user.ID)
            {
                throw new WebServerException(WebError.InvalidParameters, "PhoneNumber does not exist or does not belong to the current user!");
            }
            if (!phone.IsVerified)
            {
                throw new WebServerException(WebError.InvalidOperation, "Cannot set primary phone number without verification!");
            }

            foreach (PhoneNumber p in authTables.PhoneNumbers.GetStructs(nameof(PhoneNumber.UserID), user.ID))
            {
                if (!p.IsPrimary)
                {
                    continue;
                }

                PhoneNumber update = p;
                update.IsPrimary = false;
                authTables.PhoneNumbers.Update(update);
            }
            phone.IsPrimary = true;
            authTables.PhoneNumbers.Update(phone);
            authTables.Save();
            data.Result.AddMessage(data.Method, "User primary phonenumber set.");
            data.Result.AddStruct(user.ClearPrivateFields());
            data.Result.AddStructs(authTables.PhoneNumbers.GetStructs(nameof(PhoneNumber.UserID), user.ID));
        }

        /// <summary>Adds a phone number.</summary>
        /// <param name="data">The data.</param>
        /// <param name="countryID">The country identifier.</param>
        /// <param name="prefix">The prefix.</param>
        /// <param name="number">The number.</param>
        /// <remarks>Returns <see cref="WebMessage"/>, <see cref="User"/>, <see cref="PhoneNumber"/></remarks>
        [WebPage(Paths = "/auth/account/phonenumber/add", AuthType = WebServerAuthType.Session)]
        public void AddPhoneNumber(WebData data, long countryID, int prefix, long number)
        {
            AuthTables authTables = data.Server.AuthTables;
            User user = data.Session.GetUser();
            PhoneNumber phone = new PhoneNumber()
            {
                UserID = user.ID,
                Prefix = prefix,
                Number = number,
                CountryID = countryID,
                IsPrimary = authTables.PhoneNumbers.Count(nameof(PhoneNumber.UserID), user.ID) == 0,
            };
            //TODO verification
            phone.ID = authTables.PhoneNumbers.Insert(phone);
            authTables.Save();
            data.Result.AddMessage(data.Method, "User phonenumber added");
            data.Result.AddStruct(user.ClearPrivateFields());
            data.Result.AddStructs(authTables.PhoneNumbers.GetStructs(nameof(PhoneNumber.UserID), user.ID));
        }

        /// <summary>Removed a phone number.</summary>
        /// <param name="data">The data.</param>
        /// <param name="phoneNumberID">The phone number identifier.</param>
        /// <exception cref="Cave.Web.WebServerException">
        /// Address does not exist or does not belong to the current user!
        /// or
        /// Cannot remove primary phone number!
        /// </exception>
        /// <remarks>Returns <see cref="WebMessage"/>, <see cref="User"/>, <see cref="PhoneNumber"/></remarks>
        [WebPage(Paths = "/auth/account/phonenumber/delete", AuthType = WebServerAuthType.Session)]
        public void DeletePhoneNumber(WebData data, long phoneNumberID)
        {
            AuthTables authTables = data.Server.AuthTables;
            User user = data.Session.GetUser();
            if (!authTables.PhoneNumbers.TryGetStruct(phoneNumberID, out PhoneNumber phone) || phone.UserID != user.ID)
            {
                throw new WebServerException(WebError.InvalidParameters, "Address does not exist or does not belong to the current user!");
            }
            if (phone.IsPrimary)
            {
                throw new WebServerException(WebError.InvalidOperation, "Cannot remove primary phone number!");
            }

            authTables.Addresses.Delete(phone.ID);
            authTables.Save();
            data.Result.AddMessage(data.Method, "User address deleted");
            data.Result.AddStruct(user.ClearPrivateFields());
            data.Result.AddStructs(authTables.PhoneNumbers.GetStructs(nameof(PhoneNumber.UserID), user.ID));
        }
        #endregion

        #region /auth/account/group
        /// <summary>
        /// Allows a user to create a group. 
        /// The user creating the group is added to the group with administrator priviledges.
        /// This requires an authenticated session.
        /// </summary>
        /// <param name="data">The data.</param>
        /// <param name="groupName">Name of the group.</param>
        /// <exception cref="WebServerException"><see cref="WebError.InvalidOperation"/> User is already member of too many groups!
        /// or
        /// <see cref="WebError.SessionRequired" /> Session Object {0} is not available at the session!</exception>
        /// <remarks>Returns <see cref="WebMessage"/> (Result), <see cref="Group"/> (Group), <see cref="GroupMember"/> (GroupMember: own group member dataset)</remarks>
        [WebPage(Paths = "/auth/account/group/create", AuthType = WebServerAuthType.Session)]
        public void CreateGroup(WebData data, string groupName)
        {
            AuthTables authTables = data.Server.AuthTables;
            User user = data.Session.GetUser();
            if (20 < authTables.GroupMembers.Count(nameof(GroupMember.UserID), user.ID))
            {
                throw new WebServerException(WebError.InvalidOperation, 0, "User is already member of too many groups!");
            }
            Group group = new Group()
            {
                Color = ARGB.Random.AsUInt32,
                AvatarID = DefaultRNG.UInt32,
                Name = groupName,
            };
            group.ID = authTables.Groups.Insert(group);
            GroupMember member = new GroupMember()
            {
                GroupID = group.ID,
                UserID = user.ID,
                Flags = GroupMemberFlags.IsAdmin,
            };
            member.ID = authTables.GroupMembers.Insert(member);
            authTables.Save();
            data.Result.AddMessage(data.Method, string.Format("Group {0} created. Administrator {1} added.", user, group));
            data.Result.AddStruct(group);
            data.Result.AddStruct(member);
        }

        /// <summary>Invites another user to a group. 
        /// The user calling this function needs to have group administrator priviledges.
        /// This requires an authenticated session.
        /// </summary>
        /// <param name="data">The data.</param>
        /// <param name="groupID">The group identifier.</param>
        /// <param name="email">The email.</param>
        /// <exception cref="WebServerException">
        /// <see cref="WebError.InvalidOperation"/> User was already invited or is part of the group!
        /// or
        /// <see cref="WebError.InvalidParameters"/> Invalid group or User {0} is not an administrator of the group!
        /// or
        /// <see cref="WebError.SessionRequired" /> Session Object {0} is not available at the session!</exception>
        /// <remarks>Returns <see cref="WebMessage"/> (Result)</remarks>
        [WebPage(Paths = "/auth/account/group/invite", AuthType = WebServerAuthType.Session)]
        public void InviteIntoGroup(WebData data, long groupID, string email)
        {
            AuthTables authTables = data.Server.AuthTables;
            User user = data.Session.GetUser();
            if (!authTables.Groups.TryGetStruct(groupID, out Group group))
            {
                throw new WebServerException(WebError.InvalidParameters, 0, string.Format("Invalid group or User {0} is not an administrator of the group!", user));
            }

            GroupMember myMemership = authTables.GetGroupMembership(user.ID, groupID);
            if (myMemership.Flags != GroupMemberFlags.IsAdmin)
            {
                throw new WebServerException(WebError.InvalidParameters, 0, string.Format("Invalid group or User {0} is not an administrator of the group!", user));
            }

            foreach (EmailAddress emailAddress in authTables.EmailAddresses.GetStructs(nameof(EmailAddress.Address), email))
            {
                if (!emailAddress.Verified)
                {
                    continue;
                }

                User invitedUser = authTables.Users[emailAddress.UserID];

                if (authTables.GroupMembers.Exist(Search.FieldEquals(nameof(GroupMember.UserID), user.ID) & Search.FieldEquals(nameof(GroupMember.GroupID), groupID)))
                {
                    throw new WebServerException(WebError.InvalidOperation, 0, "User was already invited or is part of the group!");
                }
                GroupMember newMember = new GroupMember()
                {
                    GroupID = group.ID,
                    UserID = emailAddress.UserID,
                };
                newMember.ID = authTables.GroupMembers.Insert(newMember);
                if (authMailSender.SendGroupInvitationMessage(user, new MailAddress(email), invitedUser, group))
                {
                    data.Result.AddMessage(data.Method, string.Format("User {0} invited into group {1} and email notification sent. " +
                        "The user will appear in the group after she/he has accepted the invite.", email, group));
                    authTables.Save();
                    return;
                }
                break;
            }
            throw new WebServerException(WebError.InvalidParameters, string.Format("Could not send group invitation to {0}", email));
        }

        /// <summary>
        /// Allows a user to join a group. The user has to be invited to the group by a group admin first. This requires an authenticated session.
        /// </summary>
        /// <param name="data">The data.</param>
        /// <param name="groupID">The group identifier.</param>
        /// <exception cref="WebServerException">
        /// <see cref="WebError.SessionRequired" /> Session Object {0} is not available at the session!
        /// </exception>
        /// <remarks>Returns <see cref="WebMessage"/> (Result)</remarks>
        [WebPage(Paths = "/auth/account/group/join", AuthType = WebServerAuthType.Session)]
        public void JoinGroup(WebData data, long groupID)
        {
            AuthTables authTables = data.Server.AuthTables;
            User user = data.Session.GetUser();
            GroupMember member = authTables.GetGroupMembership(user.ID, groupID);
            if (member.GroupID == groupID && member.UserID == user.ID && member.Flags == 0)
            {
                member.Flags = GroupMemberFlags.HasJoined;
                authTables.GroupMembers.Replace(member);
                data.Result.AddMessage(data.Method, string.Format("User {0} joined to group {1}.", user, groupID));
                authTables.Save();
                return;
            }
            throw new WebServerException(WebError.InvalidParameters, 0, string.Format("Invalid group or User {0} was not invited to the group!", user));
        }

        /// <summary>Allows a user to leave a group. This requires an authenticated session.</summary>
        /// <param name="data">The data.</param>
        /// <param name="groupID">The group identifier.</param>
        /// <exception cref="WebServerException"><see cref="WebError.InvalidParameters" /> Invalid group or User {0} is not a member of the group!
        /// or
        /// <see cref="WebError.SessionRequired" /> Session Object {0} is not available at the session!</exception>
        /// <remarks>Returns <see cref="WebMessage"/> (Result)</remarks>
        [WebPage(Paths = "/auth/account/group/leave", AuthType = WebServerAuthType.Session)]
        public void LeaveGroup(WebData data, long groupID)
        {
            AuthTables authTables = data.Server.AuthTables;
            User user = data.Session.GetUser();
            var members = authTables.GroupMembers.GetStructs(
                Search.FieldEquals(nameof(GroupMember.UserID), user.ID) &
                Search.FieldEquals(nameof(GroupMember.GroupID), groupID));
            if (members.Count != 1)
            {
                throw new WebServerException(WebError.InvalidParameters, 0, string.Format("Invalid group or User {0} is not a member of the group!", user));
            }
            authTables.GroupMembers.Delete(members.Select(m => m.ID).ToArray());
            data.Result.AddMessage(data.Method, "User left group.");
            authTables.Save();
        }

        #endregion

        #endregion
    }
}