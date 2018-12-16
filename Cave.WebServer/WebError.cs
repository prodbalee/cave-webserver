namespace Cave.Web
{
    /// <summary>
    /// Provides all possible error codes
    /// </summary>
    public enum WebError
    {
        /// <summary>no error</summary>
        None = 0,

        /// <summary>client error, see details at the message</summary>
        ClientError,

        /// <summary>internal server error. Please call support.</summary>
        InternalServerError,

        /// <summary>redirect to another uri</summary>
        Redirect,

        /// <summary>uri not found</summary>
        NotFound,

        /// <summary>missing rights to access the resource</summary>
        MissingRights,

        /// <summary>unknown content retrieved at get / post request</summary>
        UnknownContent,

        /// <summary>maximum size exceeded at post request or get header</summary>
        MaximumSizeExceeded,

        /// <summary>A function call error / missing parameters</summary>
        FunctionCallError,

        /// <summary>authentication required to access the resource</summary>
        AuthenticationRequired,

        /// <summary>valid session required to access the resource</summary>
        SessionRequired,

        /// <summary>invalid parameters / parameter content</summary>
        InvalidParameters,

        /// <summary>invalid operation, this cannot be done this way</summary>
        InvalidOperation,

        /// <summary>invalid transaction key specified</summary>
        InvalidTransactionKey,

        /// <summary>invalid software</summary>
        InvalidSoftware,

        /// <summary>license missing</summary>
        LicenseMissing,

        /// <summary>license exceeded</summary>
        LicenseExceeded,

        /// <summary>A required dataset is missing</summary>
        DatasetMissing,

        /// <summary>The dataset is already present</summary>
        DatasetAlreadyPresent,

        /// <summary>Too many requests per time frame from the same source</summary>
        RequestingTooFast,
    }
}
