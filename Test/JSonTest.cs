using System.IO;
using System.Threading;
using Cave.Web;
using Cave.IO;
using NUnit.Framework;
using System;
using NUnit.Framework.Constraints;

namespace Test
{
    class JSONTest
    {
        JsonReader GetReader(string data)
        {
            return new JsonReader(new string[1] { data });
        }

        [Test]
        public void ErrorTests()
        {
            Assert.Throws(typeof(NotSupportedException), delegate () { this.GetReader(String.Empty); });
            Assert.Throws(typeof(NotSupportedException), delegate () { this.GetReader(" "); });
            Assert.Throws(typeof(NotSupportedException), delegate () { this.GetReader("{"); });
            Assert.Throws(typeof(NotSupportedException), delegate () { this.GetReader("{ \"as }"); });
        }

        [Test]
        public void BasicStringTests()
        {
            // string
            Assert.AreEqual(String.Empty, (string)GetReader("\"\"").Root.Value);
            Assert.AreEqual("TestString", (string)GetReader("\"TestString\"").Root.Value);
        }
        [Test]
        public void BasicNumberTests()
        {
            // number
            Assert.AreEqual(0, (int)GetReader("0").Root.Value);
            Assert.AreEqual(3.14f, (float)GetReader("3.14").Root.Value);
        }
        [Test]
        public void BasicObjectTests()
        {
            // object
            JsonNode node;
            node = GetReader("{}").Root;
            Assert.AreEqual(String.Empty, node.Name);
            Assert.AreEqual(String.Empty, node.Value);

            node =  GetReader("{\"name\":\"value\"}").Root;
            Assert.AreEqual("name", node.Name);
            Assert.AreEqual("value", node.Value);
        }
        [Test]
        public void BasicArrayTests()
        {
            // array
            JsonNode node;
            node = GetReader("[]").Root;
            Assert.AreEqual(0, node.SubNodes.Length);
            Assert.AreEqual(0, node.Names.Length, 0);
            Assert.AreEqual(0, node.Values.Length, 0);

            node = GetReader("[1,2,3]").Root;
            Assert.AreEqual(2, node.SubNodes.Length);
            Assert.AreEqual(3, node.Names.Length);
            Assert.AreEqual(3, node.Values.Length);
            for (int i = 0; i < 3; i++)
            {
                Assert.Equals(i+1, node.Values[i]);
            }
        }

        [Test]
        public void BasicValuesTests()
        {
            // true
            Assert.AreEqual(true, (string)GetReader("true").Root.Value);
            // false
            Assert.AreEqual(false, (string)GetReader("false").Root.Value);
            // null
            Assert.AreEqual(null, (string)GetReader("null").Root.Value);
        }


        [Test]
        public void CaveJsonTest()
        {
            string jsonString = "{\"obj\":{\"o1\": \"v1\",\"o2\": \"v2\"},\"arr\":[0,1,2],\"true\": true,\"false\":false,\"null\":null}";
            var reader = GetReader(jsonString);
            JsonNode objNode = reader.Root["obj"];

            // object node
            Assert.AreEqual(JsonNodeType.Object, objNode.Type);
            Assert.AreEqual(2, objNode.Names.Length);
            Assert.AreEqual("o1", objNode.Names[0]);
            Assert.AreEqual("o2", objNode.Names[1]);
            Assert.AreEqual(2, objNode.SubNodes.Length);
            Assert.AreEqual("o1", objNode.SubNodes[0].Name);
            Assert.AreEqual("v1", objNode.SubNodes[0].Value);
            Assert.AreEqual("o2", objNode.SubNodes[1].Name);
            Assert.AreEqual("v2", objNode.SubNodes[1].Value);


            // array node
            JsonNode arrNode = reader.Root["arr"];
            Assert.AreEqual(JsonNodeType.Array, arrNode.Type);
            Assert.AreEqual(3, arrNode.Values.Length);
            Assert.AreEqual(0, arrNode.Values[0]);
            Assert.AreEqual(1, arrNode.Values[1]);
            Assert.AreEqual(2, arrNode.Values[2]);


            // value nodes
            JsonNode trueNode = reader.Root["true"];
            Assert.AreEqual(JsonNodeType.Value, trueNode.Type);
            Assert.AreEqual(true, trueNode.Value);

            JsonNode falseNode = reader.Root["false"];
            Assert.AreEqual(JsonNodeType.Value, falseNode.Type);
            Assert.AreEqual(false, falseNode.Value);

            JsonNode nullNode = reader.Root["null"];
            Assert.AreEqual(JsonNodeType.Value, nullNode.Type);
            Assert.AreEqual(null, nullNode.Value);

        }
    }
}
