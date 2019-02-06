using System.IO;
using System.Net;
using System.Threading;
using Cave;
using Cave.Auth;
using Cave.Data;
using Cave.Net;
using Cave.Web;
using NUnit.Framework;

namespace Test
{
    public class WebServerTest
    {
        [Table]
        struct TestData
        {
            [Field(Flags =Cave.FieldFlags.ID)]
            public long ID;
            [Field]
            public string Value;
        }

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
                webData.Result.AddMessage(webData.Method, "Echo success");
                TestData td = new TestData { ID = 1,  Value = value};
                webData.Result.AddStruct<TestData>(td);
            }

            [WebPage(Paths = "basicauth", AuthType =WebServerAuthType.Basic)]
            public void TestBasicAuth(WebData webData)
            {
                webData.Result.AddMessage(webData.Method, "Basic Auth success: ");
            }


        }

        public WebServer Server { get; private set; }

        public WebServerTest()
        {
            Server = new WebServer();
            Server.EnableExplain = true;
            Server.EnableFileListing = true;
            Server.SessionMode = WebServerSessionMode.Cookie;

            User user;
            EmailAddress email;

            Server.AuthTables.CreateUser("user", "user@localhost", "password", UserState.Confirmed, 10, out user, out email);

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

        [Test]
        public void GetTestPageXML()
        {
            XmlRequest request = XmlRequest.Prepare("http://localhost:8080", "testpage");
            WebMessage message = request.Get();
            Assert.AreEqual(HttpStatusCode.OK, message.Code);
            Assert.AreEqual("Test Page success", message.Content);
        }

        [Test]
        public void GetEchoXML()
        {
            XmlRequest request = XmlRequest.Prepare("http://localhost:8080", "testecho", "value=teststring");
            WebMessage message = request.Get();
            Assert.AreEqual(HttpStatusCode.OK, message.Code);
            Assert.AreEqual("Echo success", message.Content);
            var dataTable = request.Result.GetTable<TestData>();
            Assert.AreEqual(1, dataTable.RowCount);
            TestData data = dataTable.GetStruct("ID", 1);
            Assert.AreEqual(1, data.ID);
            Assert.AreEqual("teststring", data.Value);

        }

        [Test]
        public void BasicAuth()
        {
            string authdata = "Basic " + Base64.Default.Encode("user:password");
            HttpWebRequest request = System.Net.WebRequest.CreateHttp("http://localhost:8080/basicauth");
            request.Headers.Add("Authorization", authdata);
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }


    }
}