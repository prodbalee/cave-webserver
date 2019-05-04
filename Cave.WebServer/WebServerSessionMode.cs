namespace Cave.Web
{
    /// <summary>
    /// Provides embedded web server session modes.
    /// </summary>
    public enum WebServerSessionMode
    {
        /// <summary>No sessions</summary>
        None = 0,

        /// <summary>Use sender identifier (ip and user agent)</summary>
        SenderID = 1,

        /// <summary>Use cookies</summary>
        Cookie = 2,
    }
}
