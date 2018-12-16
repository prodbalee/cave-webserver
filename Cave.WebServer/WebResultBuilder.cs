using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using Cave.Collections.Generic;
using Cave.Data;
using Cave.Net;

namespace Cave.Web
{
    /// <summary>
    /// Provides simple data serialization for <see cref="WebServerMethod"/>
    /// </summary>
    public class WebResultBuilder
    {
        class SerializerTable
        {
            public RowLayout Layout { get; }

            public List<Row> Rows { get; } = new List<Row>();

            public Set<long> IDs { get; } = new Set<long>();

            public SerializerTable(RowLayout layout, string name)
            {
                Name = name ?? layout.Name;
                Layout = layout;
            }

            public void AddStruct<T>(T row) where T : struct
            {
                AddRow(Row.Create(Layout, row));
            }

            public void AddRow(Row row)
            {
                long id = Layout.GetID(row);
                if (IDs.Contains(id))
                {
                    return;
                }

                IDs.Add(id);
                Rows.Add(row);
            }

            public string Name { get; }
        }

        WebMessage m_LastMessage;
        Dictionary<string, SerializerTable> m_Tables = new Dictionary<string, SerializerTable>();
        int m_MessageCount;

        /// <summary>Gets the server.</summary>
        /// <value>The server.</value>
        public WebServer Server { get; }

        /// <summary>Gets the request.</summary>
        /// <value>The request.</value>
        public WebRequest Request { get; }

        /// <summary>Initializes a new instance of the <see cref="WebResultBuilder" /> class.</summary>
        /// <param name="request">The request.</param>
        public WebResultBuilder(WebRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            Headers["Content-Language"] = request.Culture.TwoLetterISOLanguageName;
            AllowCompression = request.AllowCompression;
            TransmitLayout = request.Server.TransmitLayout;
            Server = request.Server;
            Request = request;

            switch (request.Extension?.ToLower())
            {
                default: Type = WebResultType.Html; break;
                case ".xml": Type = WebResultType.Xml; break;
                case ".json": Type = WebResultType.Json; break;
                case ".txt": Type = WebResultType.Plain; break;
            }
        }

        /// <summary>Initializes a new instance of the <see cref="WebResultBuilder" /> class.</summary>
        public WebResultBuilder(WebServer server)
        {
            if (server == null)
            {
                throw new ArgumentNullException(nameof(server));
            }

            Headers["Content-Language"] = Thread.CurrentThread.CurrentCulture.TwoLetterISOLanguageName;
            AllowCompression = false;
            TransmitLayout = server.TransmitLayout;
            Server = server;
            Type = WebResultType.Auto;
        }

        /// <summary>Adds a result message.</summary>
        /// <param name="method">The method.</param>
        /// <param name="error">The error.</param>
        /// <param name="code">The code.</param>
        /// <param name="message">The message.</param>
        /// <param name="args">The arguments.</param>
        public void AddMessage(WebServerMethod method, WebError error, HttpStatusCode code, string message, params object[] args)
        {
            AddMessage(WebMessage.Create(method, string.Format(message, args), error: error, code: code));
        }

        /// <summary>Adds a result message.</summary>
        /// <param name="source">The source.</param>
        /// <param name="error">The error.</param>
        /// <param name="code">The code.</param>
        /// <param name="message">The message.</param>
        /// <param name="args">The arguments.</param>
        public void AddMessage(string source, WebError error, HttpStatusCode code, string message, params object[] args)
        {
            AddMessage(WebMessage.Create(source, string.Format(message, args), error: error, code: code));
        }

        /// <summary>Adds a result message.</summary>
        /// <param name="method">The method.</param>
        /// <param name="error">The error.</param>
        /// <param name="message">The message.</param>
        /// <param name="args">The arguments.</param>
        public void AddMessage(WebServerMethod method, WebError error, string message, params object[] args)
        {
            AddMessage(WebMessage.Create(method, string.Format(message, args), error: error));
        }

        /// <summary>Adds a result message.</summary>
        /// <param name="source">The source.</param>
        /// <param name="error">The error.</param>
        /// <param name="message">The message.</param>
        /// <param name="args">The arguments.</param>
        public void AddMessage(string source, WebError error, string message, params object[] args)
        {
            AddMessage(WebMessage.Create(source, string.Format(message, args), error: error));
        }

        /// <summary>Adds a result message.</summary>
        /// <param name="method">The method.</param>
        /// <param name="message">The message.</param>
        /// <param name="args">The arguments.</param>
        public void AddMessage(WebServerMethod method, string message, params object[] args)
        {
            AddMessage(WebMessage.Create(method, string.Format(message, args)));
        }

        /// <summary>Adds a result message.</summary>
        /// <param name="source">The source.</param>
        /// <param name="message">The message.</param>
        /// <param name="args">The arguments.</param>
        public void AddMessage(string source, string message, params object[] args)
        {
            AddMessage(WebMessage.Create(source, string.Format(message, args)));
        }

        /// <summary>Adds a result message.</summary>
        /// <param name="ex">The ex.</param>
        public void AddMessage(WebException ex)
        {
            AddMessage(new WebMessage()
            {
                Code = ex.Code,
                Content = ex.Message,
                Error = ex.Error,
                Source = ex.Title,
            });
        }

        /// <summary>Adds a result message.</summary>
        /// <param name="message">The message.</param>
        public void AddMessage(WebMessage message)
        {
            message.ID = ++m_MessageCount;
            m_LastMessage = message;
            AddStruct(message);
        }

        /// <summary>Adds result content rows.</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="row">The row.</param>
        /// <param name="tableName">Name of the table.</param>
        /// <exception cref="System.Exception"></exception>
        public void AddStruct<T>(T row, string tableName = null) where T : struct
        {
            RowLayout layout = RowLayout.CreateTyped(typeof(T));
            if (tableName == null) { tableName = layout.Name; }
            if (!m_Tables.TryGetValue(tableName, out SerializerTable result))
            {
                m_Tables[tableName] = result = new SerializerTable(layout, tableName);
            }
            result.AddStruct(row);
        }

        /// <summary>Adds result content rows.</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="rows">The rows.</param>
        /// <param name="tableName">Name of the table.</param>
        /// <exception cref="System.Exception"></exception>
        public void AddStructs<T>(IEnumerable<T> rows, string tableName = null) where T : struct
        {
            RowLayout layout = RowLayout.CreateTyped(typeof(T));
            if (tableName == null) { tableName = layout.Name; }
            if (!m_Tables.TryGetValue(tableName, out SerializerTable result))
            {
                m_Tables[tableName] = result = new SerializerTable(layout, tableName);
            }
            foreach (T row in rows)
            {
                result.AddStruct(row);
            }
        }

        /// <summary>Adds result content rows.</summary>
        /// <param name="rows">The rows.</param>
        /// <param name="layout">The layout.</param>
        /// <param name="tableName">Name of the table.</param>
        /// <exception cref="System.Exception"></exception>
        public void AddRows(IEnumerable<Row> rows, RowLayout layout, string tableName = null)
        {
            if (tableName == null) { tableName = layout.Name; }
            if (!m_Tables.TryGetValue(tableName, out SerializerTable result))
            {
                m_Tables[tableName] = result = new SerializerTable(layout, tableName);
            }
            foreach (Row row in rows)
            {
                result.AddRow(row);
            }
        }

        /// <summary>Adds result content rows.</summary>
        /// <param name="table">The table.</param>
        /// <param name="tableName">Name of the table.</param>
        /// <exception cref="System.Exception"></exception>
        public void AddTable(ITable table, string tableName = null)
        {
            AddRows(table.GetRows(), table.Layout, tableName);
        }

        /// <summary>Sets the cache time.</summary>
        /// <param name="cacheTime">The cache time.</param>
        /// <exception cref="ArgumentOutOfRangeException">cacheTime</exception>
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

        /// <summary>The headers to transmit</summary>
        public Dictionary<string, string> Headers { get; } = new Dictionary<string, string>();

        /// <summary>Gets or sets a value indicating whether [allow compression].</summary>
        /// <value><c>true</c> if [allow compression]; otherwise, <c>false</c>.</value>
        public bool AllowCompression { get; set; }

        /// <summary>Gets or sets a value indicating whether tables transmit layout information.</summary>
        /// <value><c>true</c> if [transmit layout]; otherwise, <c>false</c>.</value>
        /// <remarks>This has to be set before calling prepare.</remarks>
        public bool TransmitLayout { get; set; }

        /// <summary>Gets or sets a value indicating whether the main object is used or not.</summary>
        /// <value><c>true</c> if [transmit layout]; otherwise, <c>false</c>.</value>
        /// <remarks>This has to be set before calling prepare.</remarks>
        public bool SkipMainObject { get; set; }

        /// <summary>Gets or sets the result type.</summary>
        /// <value>The type.</value>
        public WebResultType Type { get; set; }

        /// <summary>Gets or sets a value indicating whether [close after answer].</summary>
        /// <value><c>true</c> if [close after answer]; otherwise, <c>false</c>.</value>
        public bool CloseAfterAnswer { get; set; }

        /// <summary>Retrieves the answer.</summary>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public WebAnswer ToAnswer()
        {
            WebAnswer answer;
            switch (Type)
            {
                case WebResultType.Plain: answer = GetPlainAnswer(); break;
                case WebResultType.Xml: answer = GetXmlAnswer(); break;
                default:
                case WebResultType.Html: answer = GetHtmlAnswer(); break;
                case WebResultType.Json: answer = GetJsonAnswer(); break;
            }

            answer.AllowCompression = AllowCompression;
            answer.CloseAfterAnswer = CloseAfterAnswer;
            foreach (KeyValuePair<string, string> header in Headers)
            {
                answer.Headers[header.Key] = header.Value;
            }
            return answer;
        }

        private WebAnswer GetPlainAnswer()
        {
            using (MemoryStream ms = new MemoryStream())
            {
                foreach (KeyValuePair<string, SerializerTable> t in m_Tables)
                {
                    SerializerTable table = t.Value;
                    ms.WriteUtf8($"[Table: {table.Name}]\n");
                    CSVWriter writer = new CSVWriter(ms);
                    writer.SetLayout(table.Layout);
                    writer.WriteRows(table.Rows);
                    writer.Close();
                    ms.WriteUtf8($"#end table: {table.Name}\n\n");
                }
                return WebAnswer.Plain(Request, m_LastMessage, ms.ToArray());
            }
        }

        WebAnswer GetXmlAnswer()
        {
            XmlSerializer.Flags flags = 0;
            if (TransmitLayout)
            {
                flags |= XmlSerializer.Flags.WithLayout;
            }

            XmlSerializer xml = new XmlSerializer(Server.JsonVersion, flags);
            foreach (KeyValuePair<string, SerializerTable> t in m_Tables)
            {
                SerializerTable table = t.Value;
                xml.Serialize(table.Name, table.Layout, table.Rows);
            }

            return WebAnswer.Xml(Request, m_LastMessage, xml.ToString());
        }

        WebAnswer GetJsonAnswer()
        {
            JsonSerializer.Flags flags = 0;
            if (SkipMainObject)
            {
                flags |= JsonSerializer.Flags.SkipMainObject;
            }

            if (TransmitLayout)
            {
                flags |= JsonSerializer.Flags.WithLayout;
            }

            JsonSerializer json = new JsonSerializer(Server.JsonVersion, flags);

            foreach (KeyValuePair<string, SerializerTable> t in m_Tables)
            {
                SerializerTable table = t.Value;
                json.Serialize(table.Name, table.Layout, table.Rows);
            }
            return WebAnswer.Json(Request, m_LastMessage, json.ToString());
        }

        WebAnswer GetHtmlAnswer()
        {

            HtmlPageBuilder html;
            if (Request != null)
            {
                html = new HtmlPageBuilder(Request);
            }
            else
            {
                html = new HtmlPageBuilder();
                html.AddHeader(Properties.Resources.CaveWebServerHeaders);
                html.AddFooter($"&nbsp;<hr><address>{Server.ServerVersionString} Server at {NetTools.HostName}<br>&copy {Server.ServerCopyRight}</address>");
                string link = "/";
                html.Breadcrump.Add(new WebLink() { Link = link, Text = Server.Title });
            }

            foreach (KeyValuePair<string, SerializerTable> t in m_Tables)
            {
                SerializerTable table = t.Value;
                html.StartTable(table.Name, table.Layout);
                foreach (Row row in table.Rows)
                {
                    html.WriteRow(row);
                }
                html.CloseTable();
            }

            return html.ToAnswer(m_LastMessage);
        }
    }
}
