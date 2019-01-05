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
        public void BasicStringTests()
        {
            // string
            Assert.AreEqual(string.Empty, (string)GetReader("\"\"").Root.Value);
            Assert.AreEqual("TestString", (string)GetReader("\"TestString\"").Root.Value);
        }

        [Test]
        public void BasicNumberTests()
        {
            // number
            Assert.AreEqual(0, Convert.ToInt32(GetReader("0").Root.Value));
            Assert.AreEqual(3.14, Convert.ToDouble(GetReader("3.14").Root.Value));
        }

        [Test]
        public void BasicObjectTests()
        {
            // object
            JsonNode node;
            node = GetReader("{}").Root;
            Assert.AreEqual(string.Empty, node.Name);
            Assert.AreEqual(null, node.Value);

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

            node = GetReader("[,]").Root;


            node = GetReader("[1,2,3]").Root;
            Assert.AreEqual(0, node.SubNodes.Length);
            Assert.AreEqual(0, node.Names.Length);
            Assert.AreEqual(3, node.Values.Length);
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(i + 1, Convert.ToInt32(node.Values[i]));
            }
        }

        [Test]
        public void NestedArrayTests()
        {
            // nested array
            JsonNode node;
            node = GetReader("[[[]]]").Root;
            Assert.AreEqual(JsonNodeType.Array, node.Type);
            Assert.AreEqual(1, node.Values.Length);
            Assert.AreEqual(JsonNodeType.Array, node[0].Type);
            Assert.AreEqual(1, node[0].Values.Length);
            Assert.AreEqual(JsonNodeType.Array, node[0][0].Type);
            Assert.AreEqual(0, node[0][0].Values.Length, 0);

            node = GetReader("[[1,2,3],[4,5]]").Root;
            Assert.AreEqual(JsonNodeType.Array, node.Type);
            Assert.AreEqual(2, node.Values.Length);
            Assert.AreEqual(JsonNodeType.Array, node[0].Type);
            Assert.AreEqual(JsonNodeType.Array, node[1].Type);
            Assert.AreEqual(3, node[0].Values.Length);
            Assert.AreEqual(2, node[1].Values.Length);
            Assert.AreEqual(1, Convert.ToInt32(node[0][0].Value));
            Assert.AreEqual(2, Convert.ToInt32(node[0][1].Value));
            Assert.AreEqual(3, Convert.ToInt32(node[0][2].Value));
            Assert.AreEqual(4, Convert.ToInt32(node[1][0].Value));
            Assert.AreEqual(5, Convert.ToInt32(node[1][1].Value));
        }

        [Test]
        public void NestedObjectsTests()
        {
            // nested objects
            JsonNode node;
            node = GetReader("{\"a\":{},\"b\":{\"c\":1,\"d\":2}}").Root;
            Assert.AreEqual(JsonNodeType.Object, node.Type);
            Assert.AreEqual(2, node.SubNodes.Length);
            Assert.AreEqual(JsonNodeType.Object, node["a"].Type);
            Assert.AreEqual(0, node["a"].SubNodes.Length);
            Assert.AreEqual(JsonNodeType.Object, node["b"].Type);
            Assert.AreEqual(2, node["b"].SubNodes.Length);
            Assert.AreEqual(1, Convert.ToInt32(node["b"]["c"].Value));
            Assert.AreEqual(2, Convert.ToInt32(node["b"]["d"].Value));
        }

        [Test]
        public void NestedMixedTests()
        {
            // mixed nested objects
            JsonNode node;
            node = GetReader("[true,1,\"2\",{\"3\" : \"drei\"},[\"vier\",5,[\"6\",{\"7\":8}]]]").Root;
            Assert.AreEqual(JsonNodeType.Array, node.Type);
            Assert.AreEqual(5, node.Values.Length);

            Assert.AreEqual(JsonNodeType.Value, node[0].Type);
            Assert.AreEqual(JsonNodeType.Value, node[1].Type);
            Assert.AreEqual(JsonNodeType.Value, node[2].Type);
            Assert.AreEqual(JsonNodeType.Object, node[3].Type);
            Assert.AreEqual(JsonNodeType.Array, node[4].Type);

            Assert.AreEqual(true, node[0].Value);
            Assert.AreEqual(1, Convert.ToInt32(node[1].Value));
            Assert.AreEqual("2", node[2].Value.ToString());

            Assert.AreEqual(JsonNodeType.Value, node[3]["3"].Type);
            Assert.AreEqual("drei", node[3]["3"].Value.ToString());

            Assert.AreEqual(JsonNodeType.Value, node[4][0].Type);
            Assert.AreEqual(JsonNodeType.Value, node[4][1].Type);
            Assert.AreEqual(JsonNodeType.Array, node[4][2].Type);

            Assert.AreEqual("vier", node[4][0].Value.ToString());
            Assert.AreEqual(5, Convert.ToInt32(node[4][1].Value));

            Assert.AreEqual(JsonNodeType.Value, node[4][2][0].Type);
            Assert.AreEqual(JsonNodeType.Object, node[4][2][1].Type);

            Assert.AreEqual("6", node[4][2][0].Value.ToString());
            Assert.AreEqual(8, Convert.ToInt32(node[4][2][1]["7"].Value));
        }

        [Test]
        public void BasicValuesTests()
        {
            // true
            Assert.AreEqual(true, (bool)GetReader("true").Root.Value);
            // false
            Assert.AreEqual(false, (bool)GetReader("false").Root.Value);
            // null
            Assert.AreEqual(null, GetReader("null").Root.Value);
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

            // direct access
            Assert.AreEqual(2, Convert.ToInt32(reader.Root["arr"][2].Value));
            Assert.AreEqual("v2", reader.Root["obj"]["o2"].Value);
        }

        [Test]
        public void ErrorTests()
        {
            Assert.Throws(typeof(InvalidDataException), delegate () { this.GetReader(string.Empty); });
            Assert.Throws(typeof(InvalidDataException), delegate () { this.GetReader(" "); });
            Assert.Throws(typeof(InvalidDataException), delegate () { this.GetReader("{"); });
            Assert.Throws(typeof(InvalidDataException), delegate () { this.GetReader("["); });
            Assert.Throws(typeof(InvalidDataException), delegate () { this.GetReader("[,]"); });
            Assert.Throws(typeof(InvalidDataException), delegate () { this.GetReader("[1,]"); });
            Assert.Throws(typeof(InvalidDataException), delegate () { this.GetReader("a"); });
            Assert.Throws(typeof(EndOfStreamException), delegate () { this.GetReader("\""); });
            Assert.Throws(typeof(EndOfStreamException), delegate () { this.GetReader("\"a"); });
            Assert.Throws(typeof(EndOfStreamException), delegate () { this.GetReader("{\"a}"); });
            Assert.Throws(typeof(InvalidDataException), delegate () { this.GetReader("{\"a\"}"); });
            Assert.Throws(typeof(InvalidDataException), delegate () { this.GetReader("{\"a\":}"); });
            Assert.Throws(typeof(InvalidDataException), delegate () { this.GetReader("{\"a\":\"b\",}"); });
        }

    }
}
