using System;
using Cave.Auth;

namespace Cave.Web
{
    /// <summary>
    /// Provides embedded web server authentication event arguments.
    /// </summary>
    /// <seealso cref="EventArgs" />
    public class WebServerAuthEventArgs : EventArgs
    {
        /// <summary>Gets the web data.</summary>
        /// <value>The web data.</value>
        public WebData Data { get; }

        /// <summary>Gets the authentication type sent by the client.</summary>
        public WebServerAuthType AuthType { get; }

        /// <summary>Gets the page settings.</summary>
        /// <value>The page settings.</value>
        public WebPageAttribute Page => Data.Method.PageAttribute;

        /// <summary>Gets the username.</summary>
        /// <value>The username.</value>
        public string Username { get; }

        /// <summary>Gets the password.</summary>
        /// <value>The password.</value>
        public string Password { get; }

        /// <summary>Sets the authenticated flag to the request and the session.</summary>
        /// <param name="user">The user.</param>
        /// <param name="flags">Used internally to define local host usage.</param>
        /// <exception cref="InvalidOperationException">IsAuthenticated cannot be set twice!.</exception>
        /// <remarks>This can only be used once per request. Once set this will throw an Exception on any further calls.</remarks>
        public void SetAuthentication(User user, UserSessionFlags flags = 0)
        {
            Data.Session.SetAuthentication(user, flags);
        }

        /// <summary>Initializes a new instance of the <see cref="WebServerAuthEventArgs" /> class.</summary>
        public WebServerAuthEventArgs(WebData data)
        {
            Data = data;

            // basic auth ?
            data.Request.Headers.TryGetValue("authorization", out string value);
            if (value != null)
            {
                string[] auth = value.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                switch (auth[0].ToLower())
                {
                    case "basic":
                        string[] parts = Base64.Default.DecodeUtf8(auth[1]).Split(new char[] { ':' }, 2);
                        Username = parts[0];
                        Password = parts[1];
                        AuthType = WebServerAuthType.Basic;
                        return;
                    default:
                        throw new NotImplementedException();
                }
            }
        }
    }
}
