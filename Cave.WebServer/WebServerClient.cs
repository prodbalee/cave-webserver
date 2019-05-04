using System;
using System.Diagnostics;
using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;
using Cave.Compression;
using Cave.IO;
using Cave.Net;

namespace Cave.Web
{
    class WebServerClient : TcpAsyncClient, IWebClient
    {
        internal volatile WebServer WebServer;

        public WebServerClient()
        {
        }

        protected override void OnConnect()
        {
            base.OnConnect();
            Task.Factory.StartNew(() =>
            {
                if (Stream == null)
                {
                    return;
                }

                // perform ssl handshake
                SslHandshake();

                // handle client
                WebServer.HandleClient(this);
            }, TaskCreationOptions.LongRunning);
        }

        void SslHandshake()
        {
            if (WebServer.Certificate == null)
            {
                Reader = new DataReader(Stream, newLineMode: NewLineMode.CRLF);
                Writer = new DataWriter(Stream, newLineMode: NewLineMode.CRLF);
                return;
            }
            try
            {
                var sslStream = new SslStream(Stream);
                sslStream.AuthenticateAsServer(WebServer.Certificate);
                Reader = new DataReader(sslStream, newLineMode: NewLineMode.CRLF);
                Writer = new DataWriter(sslStream, newLineMode: NewLineMode.CRLF);
                if (WebServer.PerformanceChecks)
                {
                    Trace.TraceInformation("SslHandshake completed. Elapsed {0}.", StopWatch.Elapsed.FormatTime());
                }
            }
            catch (Exception ex)
            {
                if (WebServer.PerformanceChecks)
                {
                    Trace.TraceError("SslHandshake <red>error<default> {1}. Elapsed {0}.", StopWatch.Elapsed.FormatTime(), ex);
                }
                var data = new WebData(WebServer, StopWatch);
                data.Result.AddMessage("SslHandshake", WebError.ClientError, $"Http connections are not supported!");
                data.Result.Type = WebResultType.Html;
                data.Result.CloseAfterAnswer = true;
                SendAnswer(data);
            }
        }

        public void SendAnswer(WebData data)
        {
            if (data.Answer == null)
            {
                if (data.Result == null)
                {
                    throw new Exception("Al least one of Result or Answer has to be set!");
                }

                data.Answer = data.Result.ToAnswer();
                data.Result = null;
            }
            else if (data.Result != null)
            {
                // add all headers present at result to answer.
                foreach (System.Collections.Generic.KeyValuePair<string, string> header in data.Result.Headers)
                {
                    if (!data.Answer.Headers.ContainsKey(header.Key))
                    {
                        data.Answer.Headers[header.Key] = header.Value;
                    }
                }
            }

            data.Answer.Headers["To"] = data.Request.SourceAddress;
            if (!data.Answer.Headers.ContainsKey("Cache-Control"))
            {
                data.Answer.Headers["Cache-Control"] = "no-cache, must-revalidate, post-check=0, pre-check=0";
            }
            if (data.Session != null)
            {
                switch (WebServer.SessionMode)
                {
                    case WebServerSessionMode.Cookie:
                        data.Answer.Headers["Session"] = data.Session.ID.ToString();
                        if (WebServer.SessionMode == WebServerSessionMode.Cookie)
                        {
                            data.Answer.Headers["Set-Cookie"] = $"Session={data.Session.ID}; Path=/; Max-Age=" + (int)data.Server.SessionTimeout.TotalSeconds;
                        }

                        break;
                    case WebServerSessionMode.SenderID:
                        data.Answer.Headers["Session"] = data.Session.ID.ToString();
                        break;
                    case WebServerSessionMode.None: break;
                    default: throw new NotImplementedException();
                }
            }
            SendAnswer(data.Answer);
        }

        /// <summary>Writes the answer.</summary>
        /// <param name="answer">The answer.</param>
        public void SendAnswer(WebAnswer answer)
        {
            Writer.WriteLine($"HTTP/1.1 {(int)answer.StatusCode} {answer.StatusCode}");
            if (answer == null)
            {
                answer = WebAnswer.Empty;
            }

            answer.Headers["Date"] = DateTime.Now.ToString("R");
            answer.Headers["Server"] = "CaveSystems WebServer";
            answer.Headers["Connection"] = answer.CloseAfterAnswer ? "close" : "persistent";
            if (!WebServer.DisableCompression && (WebServer.ForceCompression || answer.AllowCompression) && (answer.ContentData.Length > 128))
            {
                byte[] packed = answer.ContentData.Gzip();
                if (packed.Length < answer.ContentData.Length)
                {
                    answer.ContentData = packed;
                    answer.Headers["Content-Encoding"] = "gzip";
                }
            }
            foreach (System.Collections.Generic.KeyValuePair<string, string> header in answer.Headers)
            {
                Writer.WriteLine(header.Key + ": " + header.Value);
            }
            Writer.WriteLine();
            if (answer.ContentData != null)
            {
                Writer.Write(answer.ContentData);
            }
            Trace.TraceInformation("{0}. Elapsed <cyan>{1}<default>.", answer, StopWatch.Elapsed.FormatTime());
            if (answer.CloseAfterAnswer)
            {
                DateTime endTime = DateTime.UtcNow.AddSeconds(10);
                while (IsConnected && DateTime.UtcNow < endTime)
                {
                    Thread.Sleep(1);
                }

                Close();
            }
        }

        public Stopwatch StopWatch { get; } = Stopwatch.StartNew();
        public DataWriter Writer { get; internal set; }
        public DataReader Reader { get; internal set; }

        public override string ToString()
        {
            return RemoteEndPoint.ToString();
        }
    }
}
