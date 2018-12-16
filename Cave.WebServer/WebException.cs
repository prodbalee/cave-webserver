using System;
using System.Net;

namespace Cave.Web
{
    /// <summary>
    /// embedded web server exception message
    /// </summary>
    /// <seealso cref="Exception" />
    public class WebException : Exception
    {
        /// <summary>Gets the error.</summary>
        /// <value>The error.</value>
        public WebError Error { get; }

        /// <summary>Gets the code.</summary>
        /// <value>The code.</value>
        public HttpStatusCode Code { get; }

        /// <summary>Gets the title.</summary>
        /// <value>The title.</value>
        public string Title => Error == WebError.None ? "Success" : Error.ToString().SplitCamelCase().Join(" ");

        /// <summary>Initializes a new instance of the <see cref="WebException" /> class.</summary>
        /// <param name="error">The error.</param>
        /// <param name="message">The message.</param>
        public WebException(WebError error, string message) : base(message)
        {
            Error = error;
        }

        /// <summary>Initializes a new instance of the <see cref="WebException" /> class.</summary>
        /// <param name="error">The error.</param>
        /// <param name="message">The message.</param>
        /// <param name="args">The arguments.</param>
        public WebException(WebError error, string message, params object[] args) : base(string.Format(message, args))
        {
            Error = error;
        }

        /// <summary>Initializes a new instance of the <see cref="WebException" /> class.</summary>
        /// <param name="error">The error.</param>
        /// <param name="code">The code.</param>
        /// <param name="message">The message.</param>
        public WebException(WebError error, HttpStatusCode code, string message) : base(message)
        {
            Error = error;
            Code = code;
        }

        /// <summary>Initializes a new instance of the <see cref="WebException" /> class.</summary>
        /// <param name="error">The error.</param>
        /// <param name="code">The code.</param>
        /// <param name="message">The message.</param>
        /// <param name="args">The arguments.</param>
        public WebException(WebError error, HttpStatusCode code, string message, params object[] args) : base(string.Format(message, args))
        {
            Error = error;
            Code = code;
        }

        /// <summary>Initializes a new instance of the <see cref="WebException" /> class.</summary>
        /// <param name="ex">The inner exception.</param>
        /// <param name="error">The error.</param>
        /// <param name="code">The code.</param>
        /// <param name="message">The message.</param>
        public WebException(Exception ex, WebError error, HttpStatusCode code, string message) : base(message, ex)
        {
            Error = error;
            Code = code;
        }

        /// <summary>Initializes a new instance of the <see cref="WebException" /> class.</summary>
        /// <param name="ex">The inner exception.</param>
        /// <param name="error">The error.</param>
        /// <param name="code">The code.</param>
        /// <param name="message">The message.</param>
        /// <param name="args">The arguments.</param>
        public WebException(Exception ex, WebError error, HttpStatusCode code, string message, params object[] args) : base(string.Format(message, args), ex)
        {
            Error = error;
            Code = code;
        }

        /// <summary>Initializes a new instance of the <see cref="WebException"/> class.</summary>
        /// <param name="message">The message.</param>
        public WebException(WebMessage message) : base(message.Content)
        {
            Error = message.Error;
            Code = message.Code;
        }
    }
}