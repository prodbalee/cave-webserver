using System;
using System.IO;
using System.Net;
using Cave;
using Cave.Auth;
using Cave.Data;
using Cave.Web;
using Cave.Web.Auth;
using NUnit.Framework;

namespace Test
{
    [TestFixture]
    public class WebServerTest
    {
        [Table]
        struct TestData
        {
            [Field(Flags = Cave.FieldFlags.ID)]
            public long ID;

            // string
            [Field]
            public string vString;

            // unsigned
            [Field]
            public byte vByte;

            [Field]
            public ushort vUShort;

            [Field]
            public uint vUInt;

            [Field]
            public ulong vULong;

            // signed values
            [Field]
            public sbyte vSByte;

            [Field]
            public short vShort;

            [Field]
            public int vInt;

            [Field]
            public long vLong;

            // other types
            [Field]
            public bool vBool;

            [Field]
            public DateTime vDateTime;

            [Field]
            public TimeSpan vTimeSpan;
        }

        static TestData TestDataMin()
        {
            return new TestData
            {
                ID = 1,
                vString = string.Empty,
                vByte = byte.MinValue,
                vUShort = ushort.MinValue,
                vUInt = uint.MinValue,
                vULong = ulong.MinValue,
                vSByte = sbyte.MinValue,
                vShort = short.MinValue,
                vInt = int.MinValue,
                vLong = long.MinValue,
                vBool = false,
                vDateTime = DateTime.MinValue,
                vTimeSpan = TimeSpan.MinValue,
            };
        }

        static TestData TestDataMax()
        {
            return new TestData
            {
                ID = long.MaxValue,
                vString = "MAX",
                vByte = byte.MaxValue,
                vUShort = ushort.MaxValue,
                vUInt = uint.MaxValue,
                vULong = ulong.MaxValue,
                vSByte = sbyte.MaxValue,
                vShort = short.MaxValue,
                vInt = int.MaxValue,
                vLong = long.MaxValue,
                vBool = true,
                vDateTime = DateTime.MaxValue,
                vTimeSpan = TimeSpan.MaxValue,
            };
        }


        public enum UserLevel
        {
            /// <summary>The user flag</summary>
            User = 0,

            /// <summary>The admin flag</summary>
            Admin = 0x1000,
        }

        class TestPages
        {
            [WebPage(Paths = "testpage")]
            public void TestPage(WebData webData)
            {
                webData.Result.AddMessage(webData.Method, "Test Page success");
            }

            [WebPage(Paths = "testecho")]
            public void TestEcho(WebData webData, string value)
            {
                webData.Result.AddMessage(webData.Method, "Echo success");
                webData.Result.AddMessage("value", value);
            }

            [WebPage(Paths = "testdata")]
            public void TestData(WebData webData, string structtype = null)
            {
                webData.Result.AddMessage(webData.Method, "Test Struct success");
                TestData t = TestDataMin();
                if (structtype == "max")
                {
                    t = TestDataMax();
                }
                webData.Result.AddStruct(t);
            }

            [WebPage(Paths = "basicauth", AuthType = WebServerAuthType.Basic)]
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

            Server.TransmitLayout = false;

            User user;
            EmailAddress email;

            Server.AuthTables.CreateUser("user", "user@localhost", "password", UserState.Confirmed, 10, out user, out email);

            Server.StaticFilesPath = Directory.GetCurrentDirectory();

            Server.Register(new TestPages());

            var authInterface = new AuthInterface<UserLevel>(Server);
            Server.Register(authInterface);            

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
            request.UserAgent = "TestWebServer_Client";
            var response = (HttpWebResponse)request.GetResponse();
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            response.Close();
        }

        [Test]
        public void GetTestPageXML()
        {
            var request = XmlRequest.Prepare("http://localhost:8080", "testpage");
            WebMessage message = request.Get();
            Assert.AreEqual(HttpStatusCode.OK, message.Code);
            Assert.AreEqual("Test Page success", message.Content);
        }

        [Test]
        public void GetEchoXML()
        {
            var request = XmlRequest.Prepare("http://localhost:8080", "testecho", "value=teststring");
            WebMessage message = request.Get();
            Assert.AreEqual(HttpStatusCode.OK, message.Code);
            Assert.AreEqual("value", message.Source);
            Assert.AreEqual("teststring", message.Content);
        }

        [Test]
        public void GetTestStructMinXML()
        {
            var request = XmlRequest.Prepare("http://localhost:8080", "testdata", "structtype=min");
            WebMessage message = request.Get();
            Assert.AreEqual(HttpStatusCode.OK, message.Code);
            ITable<TestData> dataTable = request.Result.GetTable<TestData>();
            Assert.AreEqual(1, dataTable.RowCount);
            TestData data = dataTable.GetStruct(dataTable.IDs[0]);
            TestData mindata = TestDataMin();
            Assert.True(data.Equals(mindata));
        }

        [Test]
        public void GetTestStructMaxXML()
        {
            var request = XmlRequest.Prepare("http://localhost:8080", "testdata", "structtype=max");
            WebMessage message = request.Get();
            Assert.AreEqual(HttpStatusCode.OK, message.Code);
            ITable<TestData> dataTable = request.Result.GetTable<TestData>();
            Assert.AreEqual(1, dataTable.RowCount);
            TestData data = dataTable.GetStruct(dataTable.IDs[0]);
            TestData mindata = TestDataMax();
            Assert.True(data.Equals(mindata));
        }

        [Test]
        public void GetSession()
        {
            HttpWebRequest request = System.Net.WebRequest.CreateHttp("http://localhost:8080/auth/session");
            request.UserAgent = "TestWebServer_Client";
            var response = (HttpWebResponse)request.GetResponse();
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            response.Close();
        }

        [Test]
        public void GetSessionXML()
        {
            var request = XmlRequest.Prepare("http://localhost:8080", "auth/session");
            WebMessage message = request.Get();
            Assert.AreEqual(HttpStatusCode.OK, message.Code);
        }

        [Test]
        public void BasicAuth()
        {
            string authdata = "Basic " + Base64.Default.Encode("user:password");
            HttpWebRequest request = System.Net.WebRequest.CreateHttp("http://localhost:8080/basicauth");
            request.UserAgent = "TestWebServer_Client";
            request.Headers.Add("Authorization", authdata);
            var response = (HttpWebResponse)request.GetResponse();
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            response.Close();
        }
    }
}