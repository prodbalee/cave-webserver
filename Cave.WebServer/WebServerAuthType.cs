namespace Cave.Web
{
    /// <summary>
    /// Provides the server authentication type
    /// </summary>
    public enum WebServerAuthType
    {
        /// <summary>No auth</summary>
        None = 0,

        /// <summary>The basic auth with username and password</summary>
        Basic = 1,

        /// <summary>The session auth, requiring a previous basic auth</summary>
        Session = 2,
    }
}
