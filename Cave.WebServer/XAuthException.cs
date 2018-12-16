namespace Cave.Web
{
    /// <summary>
    /// Provides exceptions for XAuth
    /// </summary>
    /// <seealso cref="Cave.Web.WebException" />
    public class XAuthException : WebException
    {
        /// <summary>The request</summary>
        public XmlRequest Request { get; }

        /// <summary>Initializes a new instance of the <see cref="XAuthException"/> class.</summary>
        /// <param name="message">The message.</param>
        /// <param name="request">The request.</param>
        public XAuthException(WebMessage message, XmlRequest request) : base(message)
        {
            Request = request;
        }
    }
}