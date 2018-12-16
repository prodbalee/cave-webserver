namespace Cave.Web
{
    /// <summary>
    /// Provides available web commands
    /// </summary>
    public enum WebCommand
    {
        /// <summary>None or invalid</summary>
        None = 0,

        /// <summary>The get command</summary>
        GET,

        /// <summary>The post command</summary>
        POST,

        /// <summary>The put command</summary>
        PUT,

        /// <summary>The delete command</summary>
        DELETE,

        /// <summary>The options command</summary>
        OPTIONS,
    }
}
