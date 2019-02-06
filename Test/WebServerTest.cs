using System.IO;
using System.Net;
using System.Threading;
using Cave.Net;
using Cave.Web;
using NUnit.Framework;

namespace Test
{
    public class WebServerTest
    {
        class TestPages
        {
            [WebPage(Paths ="testpage")]
            public void TestPage(WebData webData)
            {
                webData.Result.AddMessage(webData.Method, "Test Page success");
            }

            [WebPage(Paths = "testecho")]
            public void TestEcho(WebData webData, string value)
            {
                webData.Result.AddMessage(webData.Method, "Echo success: ", value);
            }

        }

        public WebServer Server { get; private set; }

        public WebServerTest()
        {
            Server = new WebServer();
            Server.EnableExplain = true;
            Server.EnableFileListing = true;
            Server.SessionMode = WebServerSessionMode.Cookie;

            Server.StaticFilesPath = Directory.GetCurrentDirectory();

            Server.Register(new TestPages());

            Server.Listen(8080);
        }

        ~WebServerTest()
        {
            Server.Close();
        }


        [Test]
        public void GetIndex()
        {
            HttpWebRequest request = System.Net.WebRequest.CreateHttp("http://localhost:8080");
            HttpWebResponse response = (HttpWebResponse) request.GetResponse();
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }
    }
}