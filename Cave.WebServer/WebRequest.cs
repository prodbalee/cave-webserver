using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using Cave.IO;

namespace Cave.Web
{
    /// <summary>
    /// Provides a received rest request.
    /// </summary>
    public class WebRequest
    {
        /// <summary>Reads an instance using the specified reader.</summary>
        /// <param name="server">The server.</param>
        /// <param name="firstLine">The first line.</param>
        /// <param name="client">The client.</param>
        /// <returns></returns>
        /// <exception cref="WebServerException">
        /// Malformed request {0}!
        /// or
        /// Command {0} is not supported!
        /// or
        /// Protocol {0} is not supported!.
        /// </exception>
        internal static WebRequest Load(WebServer server, string firstLine, WebServerClient client)
        {
            var req = new WebRequest(server)
            {
                SourceAddress = client.RemoteEndPoint.Address.ToString(),
                LocalPort = client.LocalEndPoint.Port,
                FirstLine = firstLine,
            };

            if (server.VerboseMode)
            {
                Trace.TraceInformation($"Request {req.ID} {firstLine}");
            }

            string[] parts = req.FirstLine.Split(' ');
            if (parts.Length != 3)
            {
                throw new WebServerException(WebError.InvalidOperation, "Malformed request {0}!", req.FirstLine);
            }

            req.Command = parts[0].Parse<WebCommand>();
            switch (req.Command)
            {
                case WebCommand.DELETE:
                case WebCommand.GET:
                case WebCommand.OPTIONS:
                case WebCommand.POST:
                case WebCommand.PUT: break;
                default: throw new WebServerException(WebError.InvalidOperation, "Command {0} is not supported!", parts[0]);
            }
            switch (req.Protocol = parts[2])
            {
                case "HTTP/1.0":
                case "HTTP/1.1": break;
                default: throw new WebServerException(WebError.InvalidOperation, "Protocol {0} is not supported!", parts[2]);
            }
            var headers = new Dictionary<string, string>();
            while (true)
            {
                string line = client.Reader.ReadLine();
                if (server.VerboseMode)
                {
                    Trace.TraceInformation($"Request {req.ID} {line}");
                }

                if (string.IsNullOrEmpty(line))
                {
                    break;
                }

                string[] header = line.Split(new char[] { ':' }, 2);
                headers.Add(header[0].ToLower().Trim(), header[1].Trim());
            }
            req.Headers = new ReadOnlyDictionary<string, string>(headers);
            req.DecodeUrl(parts[1]);
            return req;
        }

        internal void LoadPost(WebServerClient client)
        {
            if (!Headers.TryGetValue("content-type", out string contentType))
            {
                return;
            }

            string contentTypeShort = contentType.BeforeFirst(';').Trim().ToLower();
            switch (contentTypeShort)
            {
                case "application/x-www-form-urlencoded": break;
                case "application/octet-stream": break;
                case "multipart/form-data": break;
                default: throw new WebServerException(WebError.UnknownContent, 0, "Unknown content type!");
            }
            int size = 0;
            {
                if (Headers.TryGetValue("content-length", out string sizeStr))
                {
                    int.TryParse(sizeStr, out size);
                }
            }

            // if (size > 20 * 1024 * 1024) throw new CaveWebException(CaveWebError.MaximumSizeExceeded, "Maximum transfer size exceeded!");
            if (Headers.ContainsKey("expect"))
            {
                if (Headers["expect"].Contains("100-continue"))
                {
                    string @continue = $"{Protocol} {(int)HttpStatusCode.Continue} {HttpStatusCode.Continue}";
                    if (Server.VerboseMode)
                    {
                        Trace.TraceInformation($"Request {ID} {@continue}");
                    }

                    client.Writer.WriteLine(@continue);
                    client.Writer.WriteLine();
                }
            }
            byte[] data = null;
            if (Headers.TryGetValue("transfer-encoding", out string transferEncoding))
            {
                switch (transferEncoding.ToLower().Trim())
                {
                    case "chunked":
                        var buf = new FifoBuffer();
                        while (true)
                        {
                            string line = client.Reader.ReadLine();
                            int chunkSize = Convert.ToInt32(line, 16);
                            if (chunkSize == 0)
                            {
                                break;
                            }

                            byte[] chunkData = client.Reader.ReadBytes(chunkSize);
                            buf.Enqueue(chunkData);
                            client.Reader.ReadLine();
                        }
                        data = buf.ToArray();
                        break;
                    default:
                        throw new WebServerException(WebError.UnknownContent, 0, string.Format("Unknown transfer encoding {0}", transferEncoding));
                }
            }
            switch (contentTypeShort)
            {
                case "application/x-www-form-urlencoded":
                    if (data != null)
                    {
                        DecodeUrl(Encoding.ASCII.GetString(data).Replace('+', ' '), true);
                    }
                    else
                    {
                        DecodeUrl(Encoding.ASCII.GetString(client.Reader.ReadBytes(size)).Replace('+', ' '), true);
                    }
                    break;
                case "application/octet-stream":
                    if (data != null)
                    {
                        SetPostData(data);
                    }
                    else
                    {
                        SetPostData(client.Reader.ReadBytes(size));
                    }
                    break;
                case "multipart/form-data":
                    if (data != null)
                    {
                        DecodeMultiPartFormData(contentType, new DataReader(new MemoryStream(data), newLineMode: NewLineMode.CRLF));
                    }
                    else
                    {
                        DecodeMultiPartFormData(contentType, client.Reader);
                    }
                    break;
                default: throw new WebServerException(WebError.UnknownContent, 0, "Unknown content type!");
            }
        }

        string id;

        /// <summary>Prevents a default instance of the <see cref="WebRequest"/> class from being created.</summary>
        private WebRequest(WebServer server)
        {
            Server = server;
        }

        /// <summary>Decodes the specified URL.</summary>
        /// <param name="url">The URL.</param>
        /// <param name="isPostData">if set to <c>true</c> [is post data].</param>
        /// <exception cref="Exception">Path already set!.</exception>
        public void DecodeUrl(string url, bool isPostData = false)
        {
            string para;
            if (isPostData)
            {
                if (DecodedUrl == null)
                {
                    throw new Exception("Main URL not set!");
                }

                PlainUrl = PlainUrl + "?" + url;
                para = url;
            }
            else
            {
                // decode url
                PlainUrl = url;
                string[] parts = url.Split(new char[] { '?' }, 2);
                if (DecodedUrl != null)
                {
                    throw new Exception("Main URL already set!");
                }

                DecodedUrl = parts[0];
                if (DecodedUrl.LastIndexOf('.') > Math.Max(1, DecodedUrl.Length - 10))
                {
                    Extension = Path.GetExtension(DecodedUrl).ToLower();
                    DecodedUrl = DecodedUrl.Substring(0, DecodedUrl.Length - Extension.Length);
                }
                para = (parts.Length > 1) ? parts[1] : null;
                while (DecodedUrl.Contains("//"))
                {
                    DecodedUrl = DecodedUrl.Replace("//", "/");
                }

                DecodedUrl = DecodedUrl.TrimEnd('/');
            }

            // get new parameter values
            var parameters = new Dictionary<string, string>();
            if (para != null)
            {
                foreach (string param in para.Split('&'))
                {
                    try
                    {
                        string[] p = param.Split(new char[] { '=' }, 2);
                        if (p.Length != 2)
                        {
                            continue;
                        }

                        parameters.Add(p[0], Uri.UnescapeDataString(p[1]).UnboxText(false));
                    }
                    catch
                    {
                        throw new WebServerException(WebError.FunctionCallError, 0, string.Format("Parameter {0} cannot be decoded!", param));
                    }
                }
            }

            // need to load old parameter values ?
            if (Parameters != null)
            {
                foreach (KeyValuePair<string, string> p in Parameters)
                {
                    if (!parameters.ContainsKey(p.Key))
                    {
                        parameters[p.Key] = p.Value;
                    }
                }
            }

            // set parameters
            Parameters = new ReadOnlyDictionary<string, string>(parameters);
        }

        /// <summary>Sets the post data.</summary>
        /// <param name="data">The data.</param>
        /// <exception cref="Exception">
        /// Request is not a post request!
        /// or
        /// PostData was already set!.
        /// </exception>
        public void SetPostData(byte[] data)
        {
            switch (Command)
            {
                case WebCommand.POST:
                case WebCommand.PUT:
                    throw new Exception("Request is not a post/put request!");
            }
            if (PostData != null)
            {
                throw new Exception("PostData was already set!");
            }

            PostData = data;
        }

        /// <summary>Decodes the form data.</summary>
        /// <param name="contentType">Type of the content.</param>
        /// <param name="reader">The reader.</param>
        /// <exception cref="WebServerException">0 - MultiPart boundary missing!.</exception>
        public void DecodeMultiPartFormData(string contentType, DataReader reader)
        {
            string boundary = contentType.AfterFirst("boundary").AfterFirst('"').BeforeFirst('"');
            if (boundary == null || boundary.HasInvalidChars(ASCII.Strings.Printable))
            {
                throw new WebServerException(WebError.UnknownContent, 0, "MultiPart boundary missing!");
            }
            MultiPartFormData = WebMultiPart.Parse(reader, boundary.Trim());
        }

        /// <summary>Gets the server.</summary>
        public WebServer Server { get; }

        /// <summary>Gets the source address (without port).</summary>
        /// <value>The source address.</value>
        public string SourceAddress { get; private set; }

        /// <summary>Gets the local port.</summary>
        /// <value>The local port.</value>
        public int LocalPort { get; private set; }

        /// <summary>Gets the first line.</summary>
        /// <value>The first line.</value>
        public string FirstLine { get; private set; }

        /// <summary>Gets the headers.</summary>
        /// <value>The headers.</value>
        public IDictionary<string, string> Headers { get; private set; }

        /// <summary>Gets the parameters.</summary>
        /// <value>The parameters.</value>
        public IDictionary<string, string> Parameters { get; internal set; }

        /// <summary>Gets the command.</summary>
        /// <value>The command.</value>
        public WebCommand Command { get; private set; }

        /// <summary>Gets the protocol (HTTP/1.0, HTTP/1.1, HTTP/2.0).</summary>
        /// <value>The protocol.</value>
        public string Protocol { get; private set; }

        /// <summary>Gets the plain URL.</summary>
        /// <value>The plain URL.</value>
        public string PlainUrl { get; private set; }

        /// <summary>Gets the path.</summary>
        /// <value>The path.</value>
        public string DecodedUrl { get; private set; }

        /// <summary>Gets the extension.</summary>
        /// <value>The extension.</value>
        public string Extension { get; private set; }

        /// <summary>Gets the culture sent by the users browser.</summary>
        /// <value>The users culture.</value>
        public CultureInfo Culture
        {
            get
            {
                if (Headers.TryGetValue("accept-language", out string language))
                {
                    foreach (string lang in language.Split(';')[0].Split(','))
                    {
                        try { return new CultureInfo(lang); } catch { }
                    }
                }
                return CultureInfo.InvariantCulture;
            }
        }

        /// <summary>Gets the post data.</summary>
        /// <value>The post data.</value>
        public byte[] PostData { get; private set; }

        /// <summary>Gets the host.</summary>
        /// <value>The host.</value>
        public string Host
        {
            get
            {
                Headers.TryGetValue("host", out string host);
                return host;
            }
        }

        /// <summary>Gets the full host address.</summary>
        /// <value>The full host address.</value>
        public string FullHost => ((Server.Certificate == null) ? "http://" : "https://") + Host;

        /// <summary>Gets the full URL.</summary>
        /// <value>The full URL.</value>
        public string FullUrl => FullHost + PlainUrl;

        /// <summary>Gets the user agent.</summary>
        /// <value>The user agent.</value>
        public string UserAgent
        {
            get
            {
                Headers.TryGetValue("user-agent", out string userAgent);
                return userAgent;
            }
        }

        /// <summary>Gets the user agent.</summary>
        /// <value>The user agent.</value>
        public string Origin
        {
            get
            {
                Headers.TryGetValue("origin", out string origin);
                return origin;
            }
        }

        /// <summary>Gets the multi part form data.</summary>
        /// <value>The multi part form data.</value>
        public WebMultiPart MultiPartFormData { get; private set; }

        /// <summary>Gets the name of the log source.</summary>
        /// <value>The name of the log source.</value>
        public string LogSourceName => "CaveWebRequest";

        /// <summary>Gets the identifier.</summary>
        /// <value>The identifier.</value>
        public string ID
        {
            get
            {
                if (id == null)
                {
                    id = Base64.NoPadding.Encode(CaveSystemData.CalculateID(FirstLine));
                }

                return id;
            }
        }

        /// <summary>Gets a value indicating whether [allow compression].</summary>
        /// <value><c>true</c> if [allow compression]; otherwise, <c>false</c>.</value>
        public bool AllowCompression => Headers.TryGetValue("accept-encoding", out string compression) && compression.ToLower().Contains("gzip");

        /// <summary>Saves the request to the specified filename (for debugging).</summary>
        /// <param name="filename">The filename.</param>
        public void Save(string filename)
        {
            using (Stream stream = File.Create(filename))
            {
                var writer = new DataWriter(stream, newLineMode: NewLineMode.CRLF);
                writer.WriteLine(FirstLine);
                foreach (KeyValuePair<string, string> head in Headers)
                {
                    writer.WriteLine(head.Key + ": " + head.Value);
                }
                writer.WriteLine();
                if (PostData != null)
                {
                    writer.Write(PostData);
                }
            }
        }

        /// <summary>Returns a <see cref="string" /> that represents this instance.</summary>
        /// <returns>A <see cref="string" /> that represents this instance.</returns>
        public override string ToString()
        {
            return FirstLine;
        }
    }
}
