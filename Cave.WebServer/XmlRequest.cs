using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace Cave.Web
{
    /// <summary>
    /// Provides a CaveXML rpc call
    /// </summary>
    public class XmlRequest
    {
        /// <summary>Creates a hash for the specified text (use salt!).</summary>
        /// <returns></returns>
        public static string CreateHash(string text, string password)
        {
            byte[] data;
            using (HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(password)))
            {
                data = hmac.ComputeHash(Encoding.UTF8.GetBytes(text));
            }
            return Base64.UrlChars.Encode(data);
        }

        /// <summary>Prepares a request to the specified server.</summary>
        /// <param name="server">The server with protocol.</param>
        /// <param name="function">The function.</param>
        /// <param name="parameters">The parameters.</param>
        /// <returns></returns>
        public static XmlRequest Prepare(string server, string function, params string[] parameters)
        {
            Uri uri = new Uri(server + "/" + function + ".xml?" + string.Join("&", parameters));
            return new XmlRequest(uri);
        }

        /// <summary>Prepares a request to the specified server.</summary>
        /// <param name="ssl">if set to <c>true</c> [SSL].</param>
        /// <param name="address">The address.</param>
        /// <param name="port">The port.</param>
        /// <param name="function">The function.</param>
        /// <param name="parameters">The parameters.</param>
        /// <returns></returns>
        public static XmlRequest Prepare(bool ssl, IPAddress address, int port, string function, params string[] parameters)
        {
            Uri uri;
            string proto = ssl ? "https://" : "http://";
            if (address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                uri = new Uri($"{proto}[{address}]:{port}/{function}.xml?{string.Join("&", parameters)}");
            }
            else
            {
                uri = new Uri($"{proto}{address}:{port}/{function}.xml?{string.Join("&", parameters)}");
            }
            return new XmlRequest(uri);
        }

        HttpWebRequest request;

        /// <summary>The header variables</summary>
        public WebHeaderCollection Headers { get; }

        /// <summary>Gets the response headers.</summary>
        /// <value>The response headers.</value>
        public ReadOnlyDictionary<string, string> ResponseHeaders { get; private set; }

        /// <summary>Gets or sets the credentials.</summary>
        /// <value>The credentials.</value>
        public NetworkCredential Credentials { get => request.Credentials as NetworkCredential; set => request.Credentials = value; }

        /// <summary>Gets the request URI.</summary>
        /// <value>The request URI.</value>
        public string RequestUri => request.RequestUri.ToString();

        /// <summary>Aborts this instance.</summary>
        public void Abort()
        {
            request?.Abort();
        }

        /// <summary>Initializes a new instance of the <see cref="XmlRequest"/> class.</summary>
        /// <param name="uri">The URI.</param>
        public XmlRequest(Uri uri)
        {
            Trace.TraceInformation("XRequest to {0}", uri);
#if NET_45
            request = WebRequest.CreateHttp(uri);
#else
            request = (HttpWebRequest)System.Net.WebRequest.Create(uri);
#endif
            Headers = request.Headers;
            request.UserAgent = $"XRequest/{CaveSystemData.VersionInfo.AssemblyVersion} ({Platform.Type}; {Platform.SystemVersionString}) .NET/{Environment.Version}";
#if !DEBUG
            Timeout = TimeSpan.FromSeconds(10);
#else
            Timeout = TimeSpan.FromSeconds(60);
#endif
        }

        /// <summary>Gets or sets the timeout.</summary>
        /// <value>The timeout.</value>
        public TimeSpan Timeout
        {
            get => new TimeSpan(request.Timeout * TimeSpan.TicksPerMillisecond);
            set
            {
                request.Timeout = (int)(value.Ticks / TimeSpan.TicksPerMillisecond);
                request.ReadWriteTimeout = request.Timeout;
            }
        }

        /// <summary>Executes a get request.</summary>
        /// <returns></returns>
        /// <exception cref="System.Exception">Already executed!</exception>
        public WebMessage Get()
        {
            if (Result != null)
            {
                throw new Exception("Already executed!");
            }

            ResponseHeaders = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
            Result = new XmlDeserializer();
            try
            {
                try
                {
                    return this.DecodeResponse(request.GetResponse());
                }
                catch (System.Net.WebException ex)
                {
                    if (ex.Response != null)
                    {
                        return this.DecodeResponse(ex.Response);
                    }

                    return WebMessage.Create(ex.Source, ex.Message, error: WebError.ClientError);
                }
            }
            catch (WebException ex)
            {
                return WebMessage.Create(ex);
            }
        }

        /// <summary>Executes a post request.</summary>
        /// <param name="data">The data.</param>
        /// <returns></returns>
        /// <exception cref="Exception">
        /// Already executed!
        /// or
        /// Incomplete transmission.
        /// </exception>
        public WebMessage Post(byte[] data = null)
        {
            if (Result != null)
            {
                throw new Exception("Already executed!");
            }

            ResponseHeaders = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
            Result = new XmlDeserializer();
            request.Method = "POST";
            if (data != null)
            {
                request.ContentType = "cave/binary";
                request.ContentLength = data.Length;
            }
            try
            {
                try
                {
                    if (data != null)
                    {
                        using (Stream s = request.GetRequestStream())
                        {
                            s.Write(data, 0, data.Length);
                            if (request.ContentLength != data.Length)
                            {
                                throw new Exception("Incomplete transmission.");
                            }
                        }
                    }
                    return this.DecodeResponse(request.GetResponse());
                }
                catch (System.Net.WebException ex)
                {
                    if (ex.Response != null)
                    {
                        return this.DecodeResponse(ex.Response);
                    }

                    return WebMessage.Create(ex.Source, ex.Message, error: WebError.ClientError);
                }
            }
            catch (WebException ex)
            {
                return WebMessage.Create(ex);
            }
        }

        private WebMessage DecodeResponse(WebResponse response)
        {
#if NET_45
            if (response.SupportsHeaders)
#endif
            {
                Dictionary<string, string> headers = new Dictionary<string, string>();
                foreach (string key in response.Headers.Keys)
                {
                    headers[key] = response.Headers[key];
                }
                ResponseHeaders = new ReadOnlyDictionary<string, string>(headers);
            }
            using (Stream s = response.GetResponseStream())
            {
                if (Debugger.IsAttached)
                {
                    //allow to view in debugger
                    using (MemoryStream ms = new MemoryStream(s.ReadAllBytes()))
                    {
                        Result.Parse(ms);
                    }
                }
                else
                {
                    Result.Parse(s);
                }
            }
            if (Result.HasTable("Result"))
            {
                return Result.GetRow<WebMessage>("Result");
            }

            return Result.GetRow<WebMessage>();
        }

        /// <summary>Deserializes this instance.</summary>
        /// <returns></returns>
        /// <exception cref="System.Exception">Use Execute() first!</exception>
        public XmlDeserializer Result { get; private set; }

        /// <summary>Returns a <see cref="string" /> that represents this instance.</summary>
        /// <returns>A <see cref="string" /> that represents this instance.</returns>
        public override string ToString()
        {
            return "XRequest " + RequestUri;
        }
    }
}
