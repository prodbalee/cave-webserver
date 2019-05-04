using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using Cave.Auth;
using Cave.Collections.Generic;
using Cave.Data;
using Cave.Net;

namespace Cave.Web
{
    /// <summary>
    /// Provides a WebServer.
    /// </summary>
    public class WebServer
    {
        /// <summary>Gets the name of the log source.</summary>
        /// <value>The name of the log source.</value>
        public const string TypeName = "CaveWebServer";

        #region private implementation

        #region private fields
        readonly Dictionary<string, WebServerMethod> paths = new Dictionary<string, WebServerMethod>();
        readonly WebExplain explain = new WebExplain();
        readonly Set<TcpServer<WebServerClient>> tcpServers = new Set<TcpServer<WebServerClient>>();

        X509Certificate2 certificate;
        IDictionary<string, WebTemplate> templates;
        bool exit;
        string copyRight;
        #endregion

        #region private functions

        void ClientAccepted(object sender, TcpServerClientEventArgs<WebServerClient> e)
        {
            e.Client.WebServer = this;
        }

        WebAnswer GetStaticFile(WebRequest request)
        {
            string url = Uri.UnescapeDataString(request.DecodedUrl).TrimStart('/');
            if (url == string.Empty)
            {
                url = "index.html";
            }
            else
            {
                url += request.Extension;
            }

            string file = FileSystem.Combine(StaticFilesPath, url);
            if (!FileSystem.IsRelative(file, StaticFilesPath))
            {
                return null;
            }

            if (!File.Exists(file))
            {
                return null;
            }

            Trace.TraceInformation("Get static file {0}", url);
            var answer = WebAnswer.Raw(
                 request,
                 WebMessage.Create(request.PlainUrl, $"<cyan>{url} <default>retrieved."),
                 File.ReadAllBytes(file),
                 MimeTypes.FromExtension(Path.GetExtension(file)));
            return answer;
        }

        void GetStaticFileListing(WebData data)
        {
            string url = Uri.UnescapeDataString(data.Request.DecodedUrl).Trim('/');
            string path = FileSystem.Combine(StaticFilesPath, url);
            var entries = new List<WebDirectoryEntry>();
            string root = FileSystem.Combine(path, "..");
            if (FileSystem.IsRelative(root, StaticFilesPath))
            {
                FileSystemInfo info = new DirectoryInfo(root);
                var entry = new WebDirectoryEntry()
                {
                    DateTime = info.LastWriteTime,
                    Name = "..",
                    Type = WebDirectoryEntryType.Directory,
                    Link = "/" + url + "/..",
                };
                entries.Add(entry);
            }
            if (FileSystem.IsRelative(path, StaticFilesPath))
            {
                foreach (string dir in Directory.GetDirectories(path))
                {
                    FileSystemInfo info = new DirectoryInfo(dir);
                    var entry = new WebDirectoryEntry()
                    {
                        DateTime = info.LastWriteTime,
                        Name = info.Name,
                        Type = WebDirectoryEntryType.Directory,
                        Link = "/" + FileSystem.Combine('/', url, info.Name),
                    };
                    entries.Add(entry);
                }
                foreach (string file in Directory.GetFiles(path))
                {
                    var info = new FileInfo(file);
                    var entry = new WebDirectoryEntry()
                    {
                        DateTime = info.LastWriteTime,
                        Size = info.Length,
                        Name = info.Name,
                        Type = WebDirectoryEntryType.File,
                        Link = "/" + FileSystem.Combine('/', url, info.Name),
                    };
                    entries.Add(entry);
                }
            }
            var pb = new HtmlPageBuilder(data.Request);
            pb.Content.CardOpenText($"File Listing:");
            pb.Content.ParagraphText($"{entries.Count} entries");
            pb.Content.TableOpen(new string[] { "Type", "Size", "Name" }, "table-striped table-responsive");
            foreach (WebDirectoryEntry entry in entries)
            {
                pb.Content.TableRowOpen();
                pb.Content.TableHtmlCell(Bootstrap4.GetBadge(entry.Type.ToString(), "badge-default"));
                pb.Content.TableCell(entry.Type == WebDirectoryEntryType.Directory ? string.Empty : entry.Size.FormatBinarySize());
                pb.Content.TableHtmlCell(Bootstrap4.GetLink(entry.Name, entry.Link));
                pb.Content.TableRowClose();
            }
            pb.Content.TableClose();
            pb.Content.CardClose();
            pb.Content.AddHtml("&nbsp;");
            data.Answer = pb.ToAnswer(WebMessage.Create("FileListing", "File listing retrieved."));
        }
        #endregion

        #region connection handling

        /// <summary>Handles a client stage1 (preparations).</summary>
        /// <remarks>Performs the firewall checks and enters stage2.</remarks>
        internal void HandleClient(WebServerClient client)
        {
            System.Globalization.CultureInfo threadCulture = Thread.CurrentThread.CurrentCulture;
            int threadId = Thread.CurrentThread.ManagedThreadId;
            WebResultBuilder result = null;
            try
            {
                // callback for connected client
                ClientConnected?.Invoke(this, new WebClientEventArgs(client));

                // do request handling
                int requestNumber = 0;
                if (PerformanceChecks)
                {
                    Trace.TraceInformation(
                        $"HandleClient [{threadId}] <cyan>{client.RemoteEndPoint}<default> ready to receive request. " +
                        $"Elapsed <cyan>{client.StopWatch.Elapsed.FormatTime()}<default>.");
                }
                while (client.IsConnected)
                {
                    result = null;
                    if (PerformanceChecks && requestNumber > 0)
                    {
                        Trace.TraceInformation(
                            $"HandleClient [{threadId}] <cyan>{client.RemoteEndPoint}<default> request <green>{requestNumber}<default> handling completed. " +
                            $"Elapsed <cyan>{client.StopWatch.Elapsed.FormatTime()}<default>.");
                    }

                    // read first request line
                    string firstLine = client.Reader.ReadLine();
                    client.StopWatch.Reset();
                    if (PerformanceChecks)
                    {
                        Trace.TraceInformation(
                            $"HandleClient [{threadId}] <cyan>{client.RemoteEndPoint}<default> start handling request <cyan>{++requestNumber}<default>. " +
                            $"Elapsed <cyan>{client.StopWatch.Elapsed.FormatTime()}<default>.");
                    }

                    // load request
                    var request = WebRequest.Load(this, firstLine, client);

                    // prepare web data object
                    var data = new WebData(request, client.StopWatch);
                    result = data.Result;

                    // update thread culture
                    Thread.CurrentThread.CurrentCulture = data.Request.Culture;

                    // handle request but change some default exceptions to web exceptions
                    try { HandleRequest(client, data); }
                    catch (ObjectDisposedException)
                    {
                        Trace.TraceInformation($"HandleClient [{threadId}] <red>{client.RemoteEndPoint}<default> Connection closed");
                    }
                    catch (InvalidOperationException ex) { throw new WebServerException(ex, WebError.InvalidOperation, 0, ex.Message); }
                    catch (ArgumentException ex) { throw new WebServerException(ex, WebError.InvalidParameters, 0, ex.Message); }
                }
            }
            catch (WebServerException ex)
            {
                Trace.TraceInformation(ex.ToString());
                if (result == null)
                {
                    result = new WebResultBuilder(this);
                }

                result.AddMessage(WebMessage.Create(ex));
                if (ex.Error == WebError.AuthenticationRequired || ex.Error == WebError.InvalidTransactionKey)
                {
                    result.Headers["WWW-Authenticate"] = $"Basic realm=\"{AssemblyVersionInfo.Program.Company} - {AssemblyVersionInfo.Program.Product}\"";
                }
                result.CloseAfterAnswer = true;
                client.SendAnswer(result.ToAnswer());
            }
            catch (SocketException)
            {
                Trace.TraceInformation($"HandleClient [{threadId}] <red>{client.RemoteEndPoint}<default> Connection closed");
                /*client closed connection*/
            }
            catch (EndOfStreamException)
            {
                /*client closed connection*/
                Trace.TraceInformation($"HandleClient [{threadId}] <red>{client.RemoteEndPoint}<default> Connection closed");
            }
            catch (Exception ex)
            {
                if (ex.InnerException is SocketException)
                {
                    Trace.TraceInformation($"HandleClient [{threadId}] <red>{client.RemoteEndPoint}<default> Connection closed");
                    return;
                }

                string supportCode = Base32.Safe.Encode(Environment.TickCount);
                Trace.TraceError("<red>Unhandled Internal Server Error<default> Code {1}\n{0}", ex.ToString(), supportCode);

                if (result == null)
                {
                    result = new WebResultBuilder(this);
                }

                result.AddMessage(ex.Source, WebError.InternalServerError, $"Internal Server Error\nUnexpected result on request.\nPlease contact support!\nSupport Code = {supportCode}");
                result.CloseAfterAnswer = true;
                client.SendAnswer(result.ToAnswer());
            }
            finally
            {
                while (client.IsConnected && client.Reader.Available == 0)
                {
                    Thread.Sleep(1);
                }

                client.Close();
                if (client != null) { ClientDisconnected?.Invoke(this, new WebClientEventArgs(client)); }

                // reset thread culture
                if (Thread.CurrentThread.CurrentCulture != threadCulture)
                {
                    Thread.CurrentThread.CurrentCulture = threadCulture;
                }
            }
        }

        void HandleRequest(WebServerClient client, WebData data)
        {
            // add acl headers
            if (Certificate != null)
            {
                data.Result.Headers["Strict-Transport-Security"] = "max-age=604800; includeSubDomains";
            }

            data.Result.Headers["Access-Control-Allow-Headers"] = "Session";
            if (data.Method?.PageAttribute?.AuthType == WebServerAuthType.Basic)
            {
                data.Result.Headers["Access-Control-Allow-Credentials"] = "true";
                data.Result.Headers["Access-Control-Allow-Headers"] += ", Authorization";
            }
            if (!data.Result.Headers.ContainsKey("Access-Control-Allow-Origin"))
            {
                data.Result.Headers["Access-Control-Allow-Origin"] = string.IsNullOrEmpty(data.Request.Origin) ? "*" : data.Request.Origin;
            }

            if (!data.Result.Headers.ContainsKey("Access-Control-Allow-Methods"))
            {
                data.Result.Headers["Access-Control-Allow-Methods"] = "POST, GET, OPTIONS";
            }

            if (data.Method?.PageAttribute?.AllowHeaders != null)
            {
                data.Result.Headers["Access-Control-Allow-Headers"] += ", " + data.Method.PageAttribute.AllowHeaders;
            }

            if (data.Request.Command == WebCommand.OPTIONS)
            {
                data.Result.AddMessage(data.Method, "Options transfered successfully.");
                client.SendAnswer(data);
                return;
            }

            data.Request.LoadPost(client);

            if (data.Method == null)
            {
                Trace.TraceInformation("Static Request: {0}", data.Request);

                if (StaticRequest != null)
                {
                    var e = new WebPageEventArgs(data);
                    StaticRequest(this, e);
                    if (e.Handled)
                    {
                        client.SendAnswer(data);
                        return;
                    }
                }

                if (EnableTemplates && RunTemplate(data))
                {
                    Trace.TraceInformation("Template: {0} {1}", data.Request, data.Result);
                    client.SendAnswer(data);
                    return;
                }

                // no method - send static file ?
                WebAnswer staticFile = GetStaticFile(data.Request);
                if (staticFile != null)
                {
                    // file present, send answer
                    Trace.TraceInformation("Static file: {0} {1}", data.Request, staticFile);
                    SetStaticCacheTime(staticFile, StaticPathCacheTime);
                    client.SendAnswer(staticFile);
                    return;
                }

                // static path access -> set cache time
                SetStaticCacheTime(data, StaticPathCacheTime);

                // file not present, check special functions
                if (EnableExplain && (data.Request.DecodedUrl.ToLower() == "/explain" || data.Request.DecodedUrl.ToLower() == "/functionlist"))
                {
                    // special page (function list / explain)
                    explain.Explain(data);
                }
                else if (EnableFileListing)
                {
                    // list files
                    GetStaticFileListing(data);
                }
                else
                {
                    // no static -> error
                    data.Result.AddMessage(data.Request.PlainUrl, WebError.NotFound, $"The requested URL {data.Request.DecodedUrl} was not found on this server.");
                }
                client.SendAnswer(data);
                return;
            }

            // invoke method
            CallMethod(data);

            // send answer
            client.SendAnswer(data);
        }

        private bool RunTemplate(WebData data)
        {
            IDictionary<string, WebTemplate> templates = this.templates;
            if (templates == null)
            {
                return false;
            }

            string key = Uri.UnescapeDataString(data.Request.DecodedUrl).TrimStart('/');
            if (key == string.Empty)
            {
                key = "index";
            }

            if (!templates.TryGetValue(key, out WebTemplate caveWebTemplate))
            {
                string str = FileSystem.Combine(data.Server.StaticFilesPath, key + ".cwt");
                if (!FileSystem.IsRelative(str, data.Server.StaticFilesPath))
                {
                    throw new WebServerException(WebError.NotFound, 0, string.Format("{0} not found!", key));
                }
                if (!File.Exists(str))
                {
                    return false;
                }

                templates[key] = caveWebTemplate = new WebTemplate(data.Server, str);
            }
            return caveWebTemplate.Render(data);
        }

        #endregion

        #endregion

        #region public events

        /// <summary>The client connected event.</summary>
        public EventHandler<WebClientEventArgs> ClientConnected;

        /// <summary>The client disconnected event.</summary>
        public EventHandler<WebClientEventArgs> ClientDisconnected;
        #endregion

        #region public properties

        /// <summary>Gets the authentication tables.</summary>
        /// <value>The authentication tables.</value>
        public AuthTables AuthTables { get; } = new AuthTables();

        /// <summary>Gets or sets the json version.</summary>
        /// <value>The json version.</value>
        public int JsonVersion { get; set; } = 3;

        /// <summary>Gets or sets a value indicating whether (gzip) compression is always used or not.</summary>
        /// <remarks><see cref="DisableCompression"/> is stronger than this property. Make sure you did not set <see cref="DisableCompression"/>.</remarks>
        /// <value><c>true</c> if [force compression]; otherwise, <c>false</c>.</value>
        public bool ForceCompression { get; set; }

        /// <summary>Gets or sets a value indicating whether (gzip) compression is disabled.</summary>
        /// <value><c>true</c> if [disabled compression]; otherwise, <c>false</c>.</value>
        public bool DisableCompression { get; set; }

        /// <summary>Gets or sets a value indicating whether tables are transmited containg layout information.</summary>
        /// <value><c>true</c> if [transmit layout]; otherwise, <c>false</c>.</value>
        public bool TransmitLayout { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether [performance checks are enabled].</summary>
        /// <value><c>true</c> if [performance checks are enabled]; otherwise, <c>false</c>.</value>
        public bool PerformanceChecks { get; set; }

        /// <summary>Gets all registered paths.</summary>
        /// <value>The paths.</value>
        public IDictionary<string, WebServerMethod> RegisteredPaths => new ReadOnlyDictionary<string, WebServerMethod>(paths);

        /// <summary>Gets or sets the name of the product.</summary>
        /// <value>The name of the product.</value>
        public string Title { get; set; } = AssemblyVersionInfo.Program.Title;

        /// <summary>The session mode.</summary>
        public WebServerSessionMode SessionMode = WebServerSessionMode.None;

        /// <summary>Gets or sets the session timeout.</summary>
        /// <value>The session timeout.</value>
        public TimeSpan SessionTimeout { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>Gets or sets the static files path.</summary>
        /// <value>The static files path.</value>
        public string StaticFilesPath { get; set; } = FileSystem.Combine(FileSystem.ProgramDirectory, "files");

        /// <summary>Gets or sets a value indicating whether [verbose mode].</summary>
        /// <value><c>true</c> if [verbose mode]; otherwise, <c>false</c>.</value>
        public bool VerboseMode { get; set; }

        /// <summary>Gets or sets a value indicating whether [enable file listing].</summary>
        /// <value><c>true</c> if [enable file listing]; otherwise, <c>false</c>.</value>
        public bool EnableFileListing { get; set; }

        /// <summary>Gets the server version string.</summary>
        /// <value>The server version string.</value>
        public string ServerVersionString => $"{TypeName}/{AssemblyVersionInfo.Program} {Base32.Safe.Encode(AppDom.ProgramID)} ({Platform.Type}; {Platform.SystemVersionString}) .NET/{Environment.Version}";

        AssemblyVersionInfo VersionInfo { get; } = AssemblyVersionInfo.FromAssembly(typeof(WebServer).Assembly);

        DateTime ReleaseDate => VersionInfo.ReleaseDate;

        /// <summary>Gets or sets the server copyright string.</summary>
        /// <value>The server copyright string.</value>
        public string ServerCopyRight
        {
            get
            {
                if (copyRight == null)
                {
                    copyRight = string.Format("2012-{0} Andreas Rohleder, 2015-{0} CaveSystems GmbH", ReleaseDate.Year);
                }

                return copyRight;
            }
            set
            {
                var sb = new StringBuilder();
                sb.AppendFormat("2012-{0} Andreas Rohleder, 2015-{0} CaveSystems GmbH", ReleaseDate.Year);
                if (!string.IsNullOrEmpty(value))
                {
                    sb.Append(", ");
                }

                sb.Append(value);
                copyRight = sb.ToString();
            }
        }

        /// <summary>Gets or sets the certificate.</summary>
        /// <value>The certificate.</value>
        public X509Certificate Certificate
        {
            get => certificate;
            set
            {
                var cert = new X509Certificate2(value);
                var rsa = cert.PrivateKey as RSACryptoServiceProvider;
                rsa.ExportParameters(true);
                certificate = cert;
            }
        }

        /// <summary>Gets or sets a value indicating whether the explain system (API help) should be enabled or not.</summary>
        /// <value>Set to <c>true</c> to enable API help; otherwise, <c>false</c>.</value>
        public bool EnableExplain { get; set; }

        /// <summary>Gets or sets a value indicating whether [enable templates].</summary>
        /// <value><c>true</c> if [enable templates]; otherwise, <c>false</c>.</value>
        public bool EnableTemplates
        {
            get => templates != null;
            set
            {
                if (!value)
                {
                    templates = null;
                    return;
                }
#if NET20 || NET35
				m_Templates = new SynchronizedDictionary<string, CaveWebTemplate>();
#else
                templates = new System.Collections.Concurrent.ConcurrentDictionary<string, WebTemplate>();
#endif
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether static templates shall be used or not.
        /// </summary>
        /// <remarks>If static templates is enabled template files will not be reloaded on changes of content files. This speeds up the render
        /// process but is uncomforable during ui development.</remarks>
        /// <value><c>true</c> if [enable static templates]; otherwise, <c>false</c>.</value>
        public bool EnableStaticTemplates { get; set; }

        /// <summary>Gets the local end points.</summary>
        /// <value>The local end points.</value>
        public IPEndPoint[] LocalEndPoints => tcpServers.Select(s => s.LocalEndPoint).ToArray();

        /// <summary>Gets or sets the static path cache time.</summary>
        /// <value>The static path cache time.</value>
        public TimeSpan StaticPathCacheTime { get; set; } = TimeSpan.FromHours(1);

        /// <summary>Gets or sets a value indicating whether session source address (ip) checks are performed.</summary>
        /// <value><c>true</c> if [require session source check]; otherwise, <c>false</c>.</value>
        /// <remarks>Without session source checks the session may roam. This may increase the risk of hostile session takeover.</remarks>
        public bool RequireSessionSourceCheck { get; set; }

        #endregion

        /// <summary>Initializes a new instance of the <see cref="WebServer"/> class.</summary>
        public WebServer() { }

        #region public functions

        /// <summary>Finds the method for the specified url.</summary>
        /// <param name="url">The URL.</param>
        /// <returns>Returns a method instance or null.</returns>
        public WebServerMethod FindMethod(string url)
        {
            if (!paths.TryGetValue(url, out WebServerMethod method))
            {
                while (url.Length > 1)
                {
                    if (paths.TryGetValue(url + "/*", out method))
                    {
                        break;
                    }

                    url = url.BeforeLast("/");
                }
            }
            return method;
        }

        /// <summary>Calls the method.</summary>
        /// <param name="data">The data.</param>
        /// <exception cref="ArgumentNullException">
        /// Session
        /// or
        /// method.
        /// </exception>
        /// <exception cref="WebServerException">0.</exception>
        public void CallMethod(WebData data)
        {
            if (data?.Session == null)
            {
                throw new ArgumentNullException("Session");
            }

            if (data.Method == null)
            {
                throw new ArgumentNullException("Method");
            }

            try { data.Method.Invoke(data); }
            catch (TargetInvocationException ex)
            {
                if (ex.InnerException is WebServerException)
                {
                    throw ex.InnerException;
                }

                Trace.TraceError("Unhandled exception: {0}", ex.InnerException.ToString());
                throw new WebServerException(WebError.InternalServerError, 0, string.Format("Internal Server Error at function {0}", data.Method));
            }
        }

        /// <summary>Registers the specified handler.</summary>
        /// <param name="instance">The instance.</param>
        /// <param name="rootPath">The root path.</param>
        public void Register(object instance, string rootPath = null)
        {
            Type type = instance.GetType();
            foreach (MethodInfo methodInfo in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                WebPageAttribute page = WebServerMethod.GetPageAttribute(methodInfo);
                if (page == null)
                {
                    continue;
                }

                var method = new WebServerMethod(instance, methodInfo, rootPath);
                Register(method);
            }
        }

        /// <summary>Registers the specified handler.</summary>
        /// <param name="method">The method.</param>
        public void Register(WebServerMethod method)
        {
            // register method at the defined paths
            foreach (string p in method.FullPaths)
            {
                string path = "/" + p.TrimEnd('/');
                while (path.Contains("//"))
                {
                    path = path.Replace("//", "/");
                }

                // if method ist the index for specified the path, register it at the path root
                if (path.EndsWith("/Index"))
                {
                    string additional = path.Substring(0, path.Length - 6);
                    if (additional == string.Empty)
                    {
                        additional = "/";
                    }

                    paths.Add(additional, method);
                    Trace.TraceInformation("Registered path <cyan>{0}<default> function {1}", additional, method);
                    if (!additional.EndsWith("/"))
                    {
                        additional += "/";
                        paths.Add(additional, method);
                        Trace.TraceInformation("Registered path <cyan>{0}<default> function {1}", additional, method);
                    }
                }
                else
                {
                    paths.Add(path, method);
                    Trace.TraceInformation("Registered path <cyan>{0}<default> function {1}", path, method);
                }
            }
        }

        /// <summary>Listens the <see cref="IPEndPoint" />s or Ports read from at the specified ini.</summary>
        /// <param name="config">The configuration source.</param>
        /// <exception cref="InvalidDataException">Invalid port {0}!
        /// or
        /// Invalid ip address at endpoint {0}!
        /// or
        /// Ini file {0} does not contain a valid [Ports] or [IPEndPoints] section!
        /// or
        /// IPEndPoint {0} invalid!.</exception>
        public void Listen(ISettings config)
        {
            bool listening = false;
            foreach (string endPoint in config.ReadSection("IPEndPoints", true))
            {
                string host = "localhost";
                int i = endPoint.IndexOfAny(")]>".ToCharArray());
                if (i > -1)
                {
                    host = endPoint.Substring(0, i + 1).Replace(')', ']').Replace('>', ']').Replace('<', '[').Replace('(', '[');
                    i = endPoint.IndexOf(':', i + 1);
                }
                else
                {
                    i = endPoint.LastIndexOf(':');
                    if (i > -1)
                    {
                        host = endPoint.Substring(0, i);
                    }
                }
                if (i < 0 || !int.TryParse(endPoint.Substring(i + 1), out int port))
                {
                    throw new InvalidDataException(string.Format("IPEndPoint {0} invalid!", endPoint));
                }

                if (IPAddress.TryParse(host, out IPAddress address))
                {
                    Listen(new IPEndPoint(address, port));
                    listening = true;
                    continue;
                }
                foreach (IPAddress ipAddress in NetTools.GetAddresses(host, AddressFamily.InterNetwork))
                {
                    Listen(new IPEndPoint(ipAddress, port));
                    listening = true;
                }
                foreach (IPAddress ipAddress in NetTools.GetAddresses(host, AddressFamily.InterNetworkV6))
                {
                    Listen(new IPEndPoint(ipAddress, port));
                    listening = true;
                }
            }
            foreach (string portStr in config.ReadSection("Ports", true))
            {
                if (!int.TryParse(portStr, out int port))
                {
                    throw new InvalidDataException(string.Format("Invalid port {0}!", portStr));
                }

                Listen(port);
                listening = true;
            }
            if (!listening)
            {
                throw new InvalidDataException(string.Format("Ini file {0} does not contain a valid [Ports] or [IPEndPoints] section!", config.Name));
            }
        }

        /// <summary>Listens at the specified port.</summary>
        /// <param name="port">The port.</param>
        /// <exception cref="InvalidOperationException">Already listening!.</exception>
        public void Listen(int port)
        {
            exit = false;
            Trace.TraceInformation("Start listening at port {0}", port);
            var server = new TcpServer<WebServerClient>();
            server.ClientAccepted += ClientAccepted;
            server.Listen(port);
            tcpServers.Add(server);
        }

        /// <summary>Listens at the specified port.</summary>
        /// <param name="endPoint">The end point.</param>
        /// <exception cref="InvalidOperationException">Already listening!.</exception>
        public void Listen(IPEndPoint endPoint)
        {
            exit = false;
            Trace.TraceInformation("Start listening at endpoint <cyan>{0}", endPoint);
            var server = new TcpServer<WebServerClient>();
            server.ClientAccepted += ClientAccepted;
            server.Listen(endPoint);
            tcpServers.Add(server);
        }

        /// <summary>
        /// Pause the webserver (will no longer accept new requests).
        /// </summary>
        public void Pause()
        {
            exit = true;
        }

        /// <summary>
        /// Continues the webserver (will accept new requests again after a call to <see cref="Pause"/>).
        /// </summary>
        public void Continue()
        {
            exit = false;
        }

        /// <summary>Closes this instance.</summary>
        public void Close()
        {
            if (exit)
            {
                return;
            }

            exit = true;
            foreach (TcpServer<WebServerClient> server in tcpServers)
            {
                try { server.Close(); } catch { }
            }
            tcpServers.Clear();
        }

        /// <summary>Sets the static cache time.</summary>
        /// <param name="data">The data.</param>
        /// <param name="cacheTime">The cache time.</param>
        /// <exception cref="ArgumentOutOfRangeException">cacheTime.</exception>
        public void SetStaticCacheTime(WebData data, TimeSpan cacheTime)
        {
            if (data.Result.Headers.ContainsKey("Cache-Control"))
            {
                return;
            }

            long seconds = cacheTime.Ticks / TimeSpan.TicksPerSecond;
            if (seconds < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(cacheTime));
            }

            data.Result.Headers["Cache-Control"] = $"public, max-age={seconds}";
            data.Result.Headers["Expires"] = (DateTime.UtcNow + cacheTime).ToString("R");
        }

        /// <summary>Sets the static cache time.</summary>
        /// <param name="answer">The answer.</param>
        /// <param name="cacheTime">The cache time.</param>
        /// <exception cref="ArgumentOutOfRangeException">cacheTime.</exception>
        public void SetStaticCacheTime(WebAnswer answer, TimeSpan cacheTime)
        {
            if (answer.Headers.ContainsKey("Cache-Control"))
            {
                return;
            }

            long seconds = cacheTime.Ticks / TimeSpan.TicksPerSecond;
            if (seconds < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(cacheTime));
            }

            answer.Headers["Cache-Control"] = $"public, max-age={seconds}";
            answer.Headers["Expires"] = (DateTime.UtcNow + cacheTime).ToString("R");
        }
        #endregion

        #region events

        /// <summary>Occurs after the session is loaded when the session check for authentication is performed.</summary>
        /// <remarks>Use this event to check for a valid user account and load the user dataset.</remarks>
        public event EventHandler<WebServerAuthEventArgs> CheckSession;

        /// <summary>Occurs after <see cref="CheckSession"/> when the user may be checked for the required rights.</summary>
        /// <remarks>Use this event to check if the authenticated user has the right to access the resource.</remarks>
        public event EventHandler<WebAccessEventArgs> CheckAccess;

        /// <summary>Occurs when [static request].</summary>
        public event EventHandler<WebPageEventArgs> StaticRequest;

        /// <summary>Raises the <see cref="E:CheckAuth" /> event.</summary>
        /// <param name="data">The data.</param>
        protected internal virtual void OnCheckSession(WebData data)
        {
            // check for basic auth in request
            if (VerboseMode)
            {
                Trace.TraceInformation("Request {0} Check Session {1}", data.Request, data.Session);
            }

            var e = new WebServerAuthEventArgs(data);
            CheckSession?.Invoke(this, e);
        }

        /// <summary>Raises the <see cref="E:CheckAccess" /> event.</summary>
        /// <param name="data">The data.</param>
        /// <exception cref="WebServerException">User does not have the right to access {0}.</exception>
        protected internal virtual void OnCheckAccess(WebData data)
        {
            if (VerboseMode)
            {
                Trace.TraceInformation("Request {0} Check Access {1}", data.Request, data.Session);
            }

            var e = new WebAccessEventArgs(data);
            CheckAccess?.Invoke(this, e);
            if (e.Denied)
            {
                throw new WebServerException(WebError.MissingRights, "User does not have the right to access {0}", data.Method);
            }
        }

        #endregion
    }
}
