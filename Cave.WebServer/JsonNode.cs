using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace Cave.IO
{
    /// <summary>
    /// Provides a Java Script Object Notation node.
    /// </summary>
    public sealed class JsonNode
    {
        /// <summary>
        /// Contains Object:JsonObject[], Array:object[] or Value:object.
        /// </summary>
        object content;

        /// <summary>
        /// Gets name of the node.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets type of the node.
        /// </summary>
        public JsonNodeType Type { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonNode"/> class.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="name"></param>
        public JsonNode(JsonNodeType type, string name)
        {
            Name = name;
            Type = type;
        }

        void CheckAdd(JsonNodeType type)
        {
            // can add anything to object type
            if (Type == JsonNodeType.Object)
            {
                return;
            }

            switch (type)
            {
                case JsonNodeType.Object:
                case JsonNodeType.Value:
                    // can add value to array
                    if (Type == JsonNodeType.Array)
                    {
                        return;
                    }

                    // can add value to value container
                    if (Type == JsonNodeType.Value)
                    {
                        return;
                    }

                    break;
            }
            throw new ArgumentException(string.Format("Cannot add item of type {0} to an JsonObject of type {1}!", type, Type));
        }

        /// <summary>
        /// Internally adds an item to the content. This may only be used for JsonNodeType.Array and JsonNodeType.Object.
        /// </summary>
        /// <param name="item"></param>
        void AddItem(object item)
        {
            ArrayList list;
            if (content == null)
            {
                list = new ArrayList();
                content = list;
            }
            else
            {
                list = (ArrayList)content;
            }
            list.Add(item);
        }

        /// <summary>
        /// Adds a subnode to this node. This may only be used for JsonNodeType.Array and JsonNodeType.Object.
        /// </summary>
        /// <param name="item"></param>
        public void Add(JsonNode item)
        {
            CheckAdd(JsonNodeType.Object);
            AddItem(item);
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
                AddItem(value);
                return;
            }
            Type = JsonNodeType.Value;
            content = value;
        }

        /// <summary>
        /// Converts this node to an array.
        /// </summary>
        internal void ConvertToArray()
        {
            if ((content != null) || (Type != JsonNodeType.Object))
            {
                throw new InvalidDataException(string.Format("Cannot convert type {0} to array!", Type));
            }

            Type = JsonNodeType.Array;
        }

        /// <summary>
        /// Gets the subnode with the specified name (object only).
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public JsonNode this[string name]
        {
            get
            {
                if (Type == JsonNodeType.Object)
                {
                    foreach (JsonNode obj in (ArrayList)content)
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
        /// Gets the value with the specified index (array only).
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public JsonNode this[int index]
        {
            get
            {
                if (Type == JsonNodeType.Array)
                {
                    object value = ((ArrayList)content)[index];
                    if (value is JsonNode node)
                    {
                        return node;
                    }

                    node = new JsonNode(JsonNodeType.Value, string.Empty);
                    node.AddValue(value);
                    return node;
                }
                return null;
            }
        }

        /// <summary>
        /// Gets the current value of the node. (This may only be used with JsonNodeType.Value).
        /// </summary>
        public object Value
        {
            get
            {
                if (Type != JsonNodeType.Value)
                {
                    return null;
                }

                return content;
            }
        }

        /// <summary>
        /// Gets the current values of the node. (This may only be used with JsonNodeType.Array).
        /// </summary>
        public object[] Values
        {
            get
            {
                if (Type == JsonNodeType.Array)
                {
                    if (content != null)
                    {
                        return ((ArrayList)content).ToArray();
                    }
                }
                return new object[0];
            }
        }

        /// <summary>
        /// Gets the current subnodes of the node. (This may only be used with JsonNodeType.Object).
        /// </summary>
        public JsonNode[] SubNodes
        {
            get
            {
                var result = new List<JsonNode>();
                if (Type == JsonNodeType.Object)
                {
                    if (content != null)
                    {
                        foreach (JsonNode obj in (ArrayList)content)
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
        /// Gets the names of all subnodes of this node. (This may only be used with JsonNodeType.Object).
        /// </summary>
        public string[] Names
        {
            get
            {
                var result = new List<string>();
                if (Type == JsonNodeType.Object)
                {
                    foreach (JsonNode obj in (ArrayList)content)
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
        /// Gets the name of the node.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return Name;
        }
    }
}
