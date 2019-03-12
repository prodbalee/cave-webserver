using System.Diagnostics;
using System.Threading;
using Cave.Web;
using Test;

namespace TestWebServer
{
    class Program
    {
        ManualResetEvent exit = new ManualResetEvent(false);

        [WebPage(Paths = "close")]
        public void Close(WebData webData)
        {
            exit.Set();
        }

        static void Main(string[] args)
        {
            Trace.Listeners.Add(new ConsoleTraceListener());
            new Program().Run();
        }

        void Run()
        {
            WebServerTest server = new WebServerTest();
            server.Server.Register(this);
            exit.WaitOne();
            // Wait for shut down
            Thread.Sleep(500);
        }
    }
}
