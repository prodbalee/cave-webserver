using System.Net;
using Cave.Data;
using Cave.IO;

namespace Cave.Web
{
    /// <summary>
    /// Provides an embedded web server result message.
    /// </summary>
    [Table("Messages")]
    public struct WebMessage
    {
        /// <summary>Creates a message using the specified eWebException.</summary>
        /// <param name="ex">The eWebException.</param>
        /// <returns></returns>
        public static WebMessage Create(WebServerException ex)
        {
            return Create(ex.Source, ex.Message, error: ex.Error, code: ex.Code);
        }

        /// <summary>Creates the specified source.</summary>
        /// <param name="source">The source.</param>
        /// <param name="message">The message.</param>
        /// <param name="error">The error.</param>
        /// <param name="code">The code.</param>
        /// <returns></returns>
        public static WebMessage Create(string source, string message, WebError error = WebError.None, HttpStatusCode code = 0)
        {
            if (code == 0)
            {
                switch (error)
                {
                    case WebError.None: code = HttpStatusCode.OK; break;
                    case WebError.Redirect: code = HttpStatusCode.RedirectKeepVerb; break;
                    case WebError.NotFound: code = HttpStatusCode.NotFound; break;

                    case WebError.MissingRights: code = HttpStatusCode.Forbidden; break;

                    case WebError.InternalServerError: code = HttpStatusCode.InternalServerError; break;

                    case WebError.InvalidTransactionKey:
                    case WebError.SessionRequired:
                    case WebError.AuthenticationRequired: code = HttpStatusCode.Unauthorized; break;

                    case WebError.RequestingTooFast: code = (HttpStatusCode)429; break;

                    default: code = HttpStatusCode.BadRequest; break;
                }
            }
            if (code == 0)
            {
                code = error == WebError.None ? HttpStatusCode.OK : HttpStatusCode.BadRequest;
            }

            return new WebMessage()
            {
                Code = code,
                Error = error,
                Content = message,
                Source = source,
            };
        }

        /// <summary>Creates a message with the specified properties.</summary>
        /// <param name="method">The method.</param>
        /// <param name="message">The message.</param>
        /// <param name="error">The error.</param>
        /// <param name="code">The code.</param>
        /// <returns>Returns a new web message.</returns>
        public static WebMessage Create(WebServerMethod method, string message, WebError error = WebError.None, HttpStatusCode code = 0)
        {
            return Create(method.Name.SplitCamelCase().Join(" "), message, error, code);
        }

        /// <summary>The identifier.</summary>
        [Field(Flags = FieldFlags.ID)]
        public long ID;

        /// <summary>The code.</summary>
        [Field]
        public WebError Error;

        /// <summary>The code.</summary>
        [Field]
        public HttpStatusCode Code;

        /// <summary>The title.</summary>
        [Field(AlternativeNames = "Title")]
        [StringFormat(StringEncoding.UTF8)]
        public string Source;

        /// <summary>The message.</summary>
        [Field(AlternativeNames = "Message")]
        [StringFormat(StringEncoding.UTF8)]
        public string Content;

        /// <summary>Returns a <see cref="string" /> that represents this instance.</summary>
        /// <returns>A <see cref="string" /> that represents this instance.</returns>
        public override string ToString()
        {
            if (Error == WebError.None)
            {
                return Source + ": " + Content;
            }

            return Error + " " + Source + ": " + Content;
        }

        /// <summary>Throws an exception for this instance if <see cref="Error"/> != <see cref="WebError.None"/>.</summary>
        public void Throw()
        {
            if (Error != WebError.None) { throw new WebServerException(this); }
        }
    }
}
