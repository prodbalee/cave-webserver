using System.IO;
using System.Net;
using System.Threading;
using Cave.Web;
using NUnit.Framework;

namespace Test
{
    public class WebServerTest
    {
        [Test]
        public void Test1()
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

            var client = new WebClient();
            var data = client.DownloadData("http://localhost:8080");

            server.Close();
        }
    }
}