using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace Cave.IO
{
    /// <summary>
    /// Provides a Java Script Object Notation node
    /// </summary>
    public sealed class JsonNode
    {
        string m_Name;
        JsonNodeType m_Type;

        /// <summary>
        /// Contains Object:JsonObject[], Array:object[] or Value:object
        /// </summary>
        object m_Content;

        /// <summary>
        /// Name of the node
        /// </summary>
        public string Name => m_Name;

        /// <summary>
        /// Type of the node
        /// </summary>
        public JsonNodeType Type => m_Type;

        /// <summary>
        /// Creates a new node
        /// </summary>
        /// <param name="type"></param>
        /// <param name="name"></param>
        public JsonNode(JsonNodeType type, string name)
        {
            m_Name = name;
            m_Type = type;
        }

        void CheckAdd(JsonNodeType type)
        {
            //can add anything to object type
            if (Type == JsonNodeType.Object)
            {
                return;
            }

            switch (type)
            {
                case JsonNodeType.Object:
                case JsonNodeType.Value:
                    //can add value to array
                    if (Type == JsonNodeType.Array)
                    {
                        return;
                    }
                    //can add value to value container
                    if (Type == JsonNodeType.Value)
                    {
                        return;
                    }

                    break;
            }
            throw new ArgumentException(string.Format("Cannot add item of type {0} to an JsonObject of type {1}!", type, Type));
        }

        /// <summary>
        /// Internally adds an item to the content. This may only be used for JsonNodeType.Array and JsonNodeType.Object
        /// </summary>
        /// <param name="item"></param>
        void m_Add(object item)
        {
            ArrayList list;
            if (m_Content == null)
            {
                list = new ArrayList();
                m_Content = list;
            }
            else
            {
                list = (ArrayList)m_Content;
            }
            list.Add(item);
        }

        /// <summary>
        /// Adds a subnode to this node. This may only be used for JsonNodeType.Array and JsonNodeType.Object
        /// </summary>
        /// <param name="item"></param>
        public void Add(JsonNode item)
        {
            CheckAdd(JsonNodeType.Object);
            m_Add(item);
        }

        /// <summary>
        /// Adds a value to this node. This may only be used for JsonNodeType.Array and JsonNodeType.Value.
        /// With JsonNodeType.Value it replaces the current value.
        /// </summary>
        /// <param name="value"></param>
        public void AddValue(object value)
        {
            CheckAdd(JsonNodeType.Value);
            if (Type == JsonNodeType.Array)
            {
                m_Add(value);
                return;
            }
            m_Type = JsonNodeType.Value;
            m_Content = value;
        }

        /// <summary>
        /// Converts this node to an array
        /// </summary>
        internal void ConvertToArray()
        {
            if ((m_Content != null) || (m_Type != JsonNodeType.Object))
            {
                throw new InvalidDataException(string.Format("Cannot convert type {0} to array!", Type));
            }

            m_Type = JsonNodeType.Array;
        }

        /// <summary>
        /// Obtains the subnode with the specified name (object only)
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public JsonNode this[string name]
        {
            get
            {
                if (Type == JsonNodeType.Object)
                {
                    foreach (JsonNode obj in (ArrayList)m_Content)
                    {
                        if (obj == null)
                        {
                            continue;
                        }

                        if (obj.Name == name)
                        {
                            return obj;
                        }
                    }
                }
                return null;
            }
        }

        /// <summary>
        /// Obtains the value with the specified index (array only)
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public JsonNode this[int index]
        {
            get
            {
                if (Type == JsonNodeType.Array)
                {
                    var value = ((ArrayList)m_Content)[index];
                    if (value is JsonNode node) return node;
                    node = new JsonNode(JsonNodeType.Value, "");
                    node.AddValue(value);
                    return node;
                }
                return null;
            }
        }

        /// <summary>
        /// Obtains the current value of the node. (This may only be used with JsonNodeType.Value)
        /// </summary>
        public object Value
        {
            get
            {
                if (Type != JsonNodeType.Value)
                {
                    return null;
                }

                return m_Content;
            }
        }

        /// <summary>
        /// Obtains the current values of the node. (This may only be used with JsonNodeType.Array)
        /// </summary>
        public object[] Values
        {
            get
            {
                if (Type == JsonNodeType.Array)
                {
                    if (m_Content != null)
                    {
                        return ((ArrayList)m_Content).ToArray();
                    }
                }
                return new object[0];
            }
        }

        /// <summary>
        /// Obtains the current subnodes of the node. (This may only be used with JsonNodeType.Object)
        /// </summary>
        public JsonNode[] SubNodes
        {
            get
            {
                List<JsonNode> result = new List<JsonNode>();
                if (Type == JsonNodeType.Object)
                {
                    if (m_Content != null)
                    {
                        foreach (JsonNode obj in (ArrayList)m_Content)
                        {
                            if (obj == null)
                            {
                                continue;
                            }

                            result.Add(obj);
                        }
                    }
                }
                return result.ToArray();
            }
        }

        /// <summary>
        /// Obtains the names of all subnodes of this node. (This may only be used with JsonNodeType.Object)
        /// </summary>
        public string[] Names
        {
            get
            {
                List<string> result = new List<string>();
                if (Type == JsonNodeType.Object)
                {
                    foreach (JsonNode obj in (ArrayList)m_Content)
                    {
                        if (obj == null)
                        {
                            continue;
                        }

                        result.Add(obj.Name);
                    }
                }
                return result.ToArray();
            }
        }

        /// <summary>
        /// Obtains the name of the node
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return Name;
        }
    }
}
