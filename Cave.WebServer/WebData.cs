using System;
using System.Diagnostics;

namespace Cave.Web
{
    /// <summary>
    /// Provides web data
    /// </summary>
    public class WebData
    {
        WebSession session;

        /// <summary>Gets the stop watch.</summary>
        /// <value>The stop watch.</value>
        internal Stopwatch StopWatch { get; private set; }

        /// <summary>The result to be sent to the client</summary>
        public WebResultBuilder Result { get; internal set; }

        /// <summary>Gets or sets the answer.</summary>
        /// <value>The answer.</value>
        public WebAnswer Answer { get; set; }

        /// <summary>The method to be called</summary>
        public WebServerMethod Method { get; internal set; }

        /// <summary>The request retrieved from the client</summary>
        public WebRequest Request { get; }

        /// <summary>The server instance</summary>
        public WebServer Server => Request.Server;

        /// <summary>Gets or sets the session.</summary>
        /// <value>The session.</value>
        public WebSession Session
        {
            get
            {
                if (session == null)
                {
                    session = WebSession.LoadSession(Request);
                }

                return session;
            }
        }

        /// <summary>Gets the name of the log source.</summary>
        /// <value>The name of the log source.</value>
        public string LogSourceName => "CaveWebData";

        /// <summary>Gets the elapsed time since request.</summary>
        /// <value>The elapsed time.</value>
        public TimeSpan Elapsed => StopWatch.Elapsed;

        /// <summary>Creates a new instance</summary>
        /// <param name="request">The request.</param>
        /// <param name="stopWatch">The stopwatch.</param>
        internal WebData(WebRequest request, Stopwatch stopWatch)
        {
            StopWatch = stopWatch;
            Request = request;
            Result = new WebResultBuilder(request);
            //check for method call
            {
                string url = request.DecodedUrl.TrimEnd('/');
                if (url.Length == 0)
                {
                    url = "/";
                }
                Method = request.Server.FindMethod(url);
                if (Method != null)
                {
                    Trace.TraceInformation("Method Call: {0} Url: {1} {2}", Method, Request, Session);
                }
            }
        }

        /// <summary>Creates a new instance</summary>
        /// <param name="server">The server.</param>
        /// <param name="stopWatch">The stop watch.</param>
        internal WebData(WebServer server, Stopwatch stopWatch)
        {
            StopWatch = stopWatch;
            Result = new WebResultBuilder(server);
        }
    }
}
