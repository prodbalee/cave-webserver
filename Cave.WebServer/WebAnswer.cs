using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Xml.Linq;

namespace Cave.Web
{
    /// <summary>Content to return to the caller.</summary>
    public class WebAnswer
    {
        static CultureInfo Default { get; set; } = new CultureInfo("en-US");

        /// <summary>Sets a html content.</summary>
        /// <param name="request">The request.</param>
        /// <param name="message">The message.</param>
        /// <param name="html">The HTML.</param>
        /// <param name="culture">The culture.</param>
        /// <returns>Returns a new RestContent instance.</returns>
        public static WebAnswer Html(WebRequest request, WebMessage message, string html, CultureInfo culture = null)
        {
            return new WebAnswer()
            {
                AllowCompression = request?.AllowCompression == true,
                Message = message,
                StatusCode = message.Code,
                ContentData = Encoding.UTF8.GetBytes(html),
                ContentType = "text/html; charset=UTF-8",
                ContentLanguage = (culture ?? Default).TwoLetterISOLanguageName,
            };
        }

        /// <summary>Sets a json content.</summary>
        /// <param name="request">The request.</param>
        /// <param name="message">The message.</param>
        /// <param name="json">The JSON.</param>
        /// <param name="culture">The culture.</param>
        /// <returns>Returns a new RestContent instance.</returns>
        public static WebAnswer Json(WebRequest request, WebMessage message, string json, CultureInfo culture = null)
        {
            return new WebAnswer()
            {
                AllowCompression = request?.AllowCompression == true,
                Message = message,
                StatusCode = message.Code,
                ContentData = Encoding.UTF8.GetBytes(json),
                ContentType = "application/json; charset=UTF-8",
                ContentLanguage = (culture ?? Default).TwoLetterISOLanguageName,
            };
        }

        /// <summary>Sets a xml content.</summary>
        /// <param name="request">The request.</param>
        /// <param name="message">The message.</param>
        /// <param name="xml">The xml.</param>
        /// <param name="culture">The culture.</param>
        /// <returns>Returns a new RestContent instance.</returns>
        public static WebAnswer Xml(WebRequest request, WebMessage message, XmlSerializer xml, CultureInfo culture = null)
        {
            return new WebAnswer()
            {
                AllowCompression = request?.AllowCompression == true,
                Message = message,
                StatusCode = message.Code,
                ContentData = xml.ToArray(),
                ContentType = "text/xml; charset=UTF-8",
                ContentLanguage = (culture ?? Default).TwoLetterISOLanguageName,
            };
        }

        /// <summary>Sets a xml content.</summary>
        /// <param name="request">The request.</param>
        /// <param name="message">The message.</param>
        /// <param name="utf8xml">The xml.</param>
        /// <param name="culture">The culture.</param>
        /// <returns>Returns a new RestContent instance.</returns>
        public static WebAnswer Xml(WebRequest request, WebMessage message, string utf8xml, CultureInfo culture = null)
        {
            return new WebAnswer()
            {
                AllowCompression = request?.AllowCompression == true,
                Message = message,
                StatusCode = message.Code,
                ContentData = Encoding.UTF8.GetBytes(utf8xml),
                ContentType = "text/xml; charset=UTF-8",
                ContentLanguage = (culture ?? Default).TwoLetterISOLanguageName,
            };
        }

        /// <summary>Sets a xml content.</summary>
        /// <param name="request">The request.</param>
        /// <param name="message">The message.</param>
        /// <param name="xdoc">The xdoc.</param>
        /// <param name="culture">The culture.</param>
        /// <returns>Returns a new RestContent instance.</returns>
        public static WebAnswer Xml(WebRequest request, WebMessage message, XDocument xdoc, CultureInfo culture = null)
        {
            byte[] data;
            using (var ms = new MemoryStream())
            using (var sw = new StreamWriter(ms, Encoding.GetEncoding(xdoc.Declaration.Encoding)))
            {
                xdoc.Save(sw);
                data = ms.ToArray();
            }
            return new WebAnswer()
            {
                AllowCompression = request?.AllowCompression == true,
                Message = message,
                StatusCode = message.Code,
                ContentData = data,
                ContentType = "text/xml; charset=" + xdoc.Declaration.Encoding,
                ContentLanguage = (culture ?? Default).TwoLetterISOLanguageName,
            };
        }

        /// <summary>Sets a plain content.</summary>
        /// <param name="request">The request.</param>
        /// <param name="message">The message.</param>
        /// <param name="content">The text.</param>
        /// <param name="culture">The culture.</param>
        /// <returns>Returns a new RestContent instance.</returns>
        public static WebAnswer Plain(WebRequest request, WebMessage message, string content, CultureInfo culture = null)
        {
            return new WebAnswer()
            {
                AllowCompression = request?.AllowCompression == true,
                Message = message,
                StatusCode = message.Code,
                ContentData = Encoding.UTF8.GetBytes(content),
                ContentType = "text/plain; charset=UTF-8",
                ContentLanguage = (culture ?? Default).TwoLetterISOLanguageName,
            };
        }

        /// <summary>Sets a plain content.</summary>
        /// <param name="request">The request.</param>
        /// <param name="message">The message.</param>
        /// <param name="data">The data.</param>
        /// <param name="culture">The culture.</param>
        /// <returns>Returns a new RestContent instance.</returns>
        public static WebAnswer Plain(WebRequest request, WebMessage message, byte[] data, CultureInfo culture = null)
        {
            return new WebAnswer()
            {
                AllowCompression = request?.AllowCompression == true,
                Message = message,
                StatusCode = message.Code,
                ContentData = data,
                ContentType = "text/plain; charset=UTF-8",
                ContentLanguage = (culture ?? Default).TwoLetterISOLanguageName,
            };
        }

        /// <summary>Sets a plain content.</summary>
        /// <param name="request">The request.</param>
        /// <param name="message">The message.</param>
        /// <param name="data">The data.</param>
        /// <param name="contentType">Type of the content.</param>
        /// <param name="culture">The culture.</param>
        /// <returns>Returns a new RestContent instance.</returns>
        public static WebAnswer Raw(WebRequest request, WebMessage message, byte[] data, string contentType, CultureInfo culture = null)
        {
            return new WebAnswer()
            {
                AllowCompression = request?.AllowCompression == true,
                Message = message,
                StatusCode = message.Code,
                ContentData = data,
                ContentType = contentType,
                ContentLanguage = (culture ?? Default).TwoLetterISOLanguageName,
            };
        }

        /// <summary>Gets an empty RestContent.</summary>
        /// <value>Empty RestContent.</value>
        public static WebAnswer Empty => new WebAnswer() { StatusCode = HttpStatusCode.NoContent, };

        /// <summary>Initializes a new instance of the <see cref="WebAnswer"/> class.</summary>
        private WebAnswer() { }

        /// <summary>Gets or sets the source (for logging only).</summary>
        /// <value>The source.</value>
        public WebMessage Message { get; set; }

        byte[] contentData;

        /// <summary>Gets or sets the data.</summary>
        /// <value>The data.</value>
        public byte[] ContentData
        {
            get => contentData;
            set
            {
                Headers["Content-Length"] = value.Length.ToString();
                contentData = value;
            }
        }

        /// <summary>Gets or sets the status code.</summary>
        /// <value>The status code.</value>
        public HttpStatusCode StatusCode { get; set; }

        /// <summary>Gets or sets the (mime) type.</summary>
        /// <value>The (mime) type.</value>
        public string ContentType
        {
            get
            {
                if (!Headers.TryGetValue("Content-Type", out string value))
                {
                    value = "text/plain; charset=UTF-8";
                }

                return value;
            }
            set => Headers["Content-Type"] = value;
        }

        /// <summary>Gets or sets the (two character iso) language.</summary>
        /// <value>The (two character iso) language.</value>
        public string ContentLanguage
        {
            get
            {
                if (!Headers.TryGetValue("Content-Language", out string value))
                {
                    value = "en";
                }

                return value;
            }
            set => Headers["Content-Language"] = value;
        }

        /// <summary>Gets or sets a value indicating whether [allow compression].</summary>
        /// <value><c>true</c> if [allow compression]; otherwise, <c>false</c>.</value>
        public bool AllowCompression { get; set; }

        /// <summary>Gets or sets a value indicating whether [close after answer].</summary>
        /// <value><c>true</c> if [close after answer]; otherwise, <c>false</c>.</value>
        public bool CloseAfterAnswer { get; set; }

        /// <summary>The headers.</summary>
        public readonly Dictionary<string, string> Headers = new Dictionary<string, string>();

        /// <summary>Sets the cache time.</summary>
        /// <param name="cacheTime">The cache time.</param>
        /// <exception cref="ArgumentOutOfRangeException">cacheTime.</exception>
        public void SetCacheTime(TimeSpan cacheTime)
        {
            if (Headers.ContainsKey("Cache-Control"))
            {
                return;
            }

            long seconds = cacheTime.Ticks / TimeSpan.TicksPerSecond;
            if (seconds < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(cacheTime));
            }

            Headers["Cache-Control"] = $"public, max-age={seconds}";
            Headers["Expires"] = (DateTime.UtcNow + cacheTime).ToString("R");
        }

        /// <summary>Returns a <see cref="string" /> that represents this instance.</summary>
        /// <returns>A <see cref="string" /> that represents this instance.</returns>
        public override string ToString()
        {
            return $"{(int)StatusCode} {StatusCode} {Message}";
        }
    }
}
