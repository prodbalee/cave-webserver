using System.IO;
using System.Threading;
using Cave.Web;

namespace TestWebServer
{
    class Program
    {
        ManualResetEvent exit = new ManualResetEvent(false);

        static void Main(string[] args)
        {
            new Program().Run();
        }

        void Run()
        {
            WebServer server = new WebServer();

            server.EnableExplain = true;
            server.EnableFileListing = true;
            server.SessionMode = WebServerSessionMode.Cookie;

            var path = Path.Combine(Directory.GetCurrentDirectory(), "html");
            for (int i = 0; i < 3 && !Directory.Exists(path); i++)
            {
                path = Path.GetFullPath(Path.Combine(path, "..", "..", "html"));
            }
            if (!Directory.Exists(path)) throw new DirectoryNotFoundException();
            server.StaticFilesPath = path;

            server.Listen(8080);

            exit.WaitOne();
            server.Close();
        }
    }
}
