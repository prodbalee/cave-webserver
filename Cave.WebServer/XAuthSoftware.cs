using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Cave.Auth;

namespace Cave.Web
{
    /// <summary>
    /// Provides authentication and software functions on auth.cave.cloud.
    /// </summary>
    public class XAuthSoftware
    {
        Timer timer;
        string password;

        WebMessage LoadSessionResult(XmlRequest request)
        {
            WebMessage message = request.Result.GetRow<WebMessage>("Result");
            if (message.Error != WebError.None)
            {
                throw new XAuthException(message, request);
            }

            Trace.TraceInformation("{0}", message);
            Software = request.Result.GetRow<Software>("Software");
            Session = request.Result.GetRow<SoftwareSession>("SoftwareSession");
            return message;
        }

        void CheckSession(object state)
        {
            try
            {
                var request = XmlRequest.Prepare(Server, "SoftwareCheckSession");
                request.Headers["Session"] = Session.SessionID.ToString();
                request.Credentials = new NetworkCredential(AssemblyVersionInfo.Program.Product + "/" + AssemblyVersionInfo.Program.AssemblyVersion + "/" + AppDom.ProgramID, password);
                WebMessage message = request.Post();
                if (message.Error != WebError.None)
                {
                    throw new XAuthException(message, request);
                }

                message = LoadSessionResult(request);
                OnSessionUpdated(new EventArgs());
            }
            catch (WebServerException ex)
            {
                Exception = ex;
                switch (ex.Error)
                {
                    case WebError.AuthenticationRequired:
                    case WebError.SessionRequired:
                    {
                        SoftwareSession session = Session;
                        session.Expiration = DateTime.UtcNow.AddSeconds(-1);
                        Session = session;
                    }
                    break;
                }
                if (true.Equals(state))
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
                Exception = ex;
                Trace.TraceError("CheckSession error!");
                if (true.Equals(state))
                {
                    throw;
                }
            }
            finally
            {
                if (Session.IsExpired)
                {
                    timer?.Dispose();
                    timer = null;
                    OnSessionUpdated(new EventArgs());
                }
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
                    SessionUpdated?.Invoke(null, e);
                });
            }
        }

        /*
        CaveXmlDeserializer CheckSession(string name, Version version, long programID, string password)
        {
            CaveXmlRequest request = CaveXmlRequest.Prepare(Server, "SoftwareCheckSession");
            request.Headers["Session"] = Session.ID.ToString();
            request.Credentials = new NetworkCredential(name + "/" + version + "/" + programID, password);
            CaveWebMessage message = request.Get();
            if (message.Error != CaveWebError.None) throw new XAuthException(message, request);
            return request.Result;
        }
        */

        /// <summary>Creates a new session.</summary>
        /// <param name="password">Password to use.</param>
        public void CreateSession(string password)
        {
            lock (this)
            {
                timer?.Dispose();
                timer = null;
                this.password = password;

                CheckSession(true);

                if (Session.SessionID > 0)
                {
                    timer = new Timer(CheckSession, null, 1000 * 10, 1000 * 10);
                }
            }
        }

        #region public properties

        /// <summary>Gets the software.</summary>
        /// <value>The software.</value>
        public Software Software { get; private set; }

        /// <summary>Gets or sets the server.</summary>
        /// <value>The server.</value>
        public string Server { get; set; } = "https://auth.cave.cloud";

        /// <summary>Occurs when [session updated].</summary>
        public event EventHandler<EventArgs> SessionUpdated;

        /// <summary>Gets the session.</summary>
        /// <value>The session.</value>
        public SoftwareSession Session { get; private set; }

        /// <summary>Gets the name of the log source.</summary>
        /// <value>The name of the log source.</value>
        public string LogSourceName => "XAuth";

        /// <summary>
        /// Gets the exception encoutered during authentication. This is null if the session is valid.
        /// </summary>
        public Exception Exception { get; private set; }

        #endregion

        /// <summary>Verifies a transaction.</summary>
        /// <param name="userSessionID">The user session identifier.</param>
        /// <param name="url">The URL.</param>
        /// <param name="transactionKey">The transaction key.</param>
        /// <returns></returns>
        public XmlDeserializer VerifyTransaction(long userSessionID, string url, string transactionKey)
        {
            if (Session.SessionID <= 0)
            {
                throw new InvalidOperationException("No valid software session!");
            }

            if (userSessionID <= 0)
            {
                throw new ArgumentNullException(nameof(userSessionID));
            }

            if (string.IsNullOrEmpty(transactionKey))
            {
                throw new ArgumentNullException(nameof(transactionKey));
            }

            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentNullException(nameof(url));
            }

            string urlHash = Base64.UrlChars.Encode(Hash.FromString(Hash.Type.SHA256, url));
            Trace.TraceInformation("Session <white>{0}<default> TransactionKey <yellow>{1}<default> URL <cyan>{2}<default> UrlHash <cyan>{3}", Base64.UrlChars.Encode(userSessionID), transactionKey, url, urlHash);
            var request = XmlRequest.Prepare(Server, "VerifyTransaction", $"userSessionID={userSessionID}", $"urlHash={urlHash}", $"transactionKey={transactionKey}");
            request.Headers["Session"] = Session.SessionID.ToString();
            WebMessage message = request.Post();
            Trace.TraceInformation(message.ToString());
            return request.Result;
        }
    }
}
