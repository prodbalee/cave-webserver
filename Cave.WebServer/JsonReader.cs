using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Cave.IO
{
    /// <summary>
    /// Provides a fast and simple json reader class.
    /// </summary>
    public class JsonReader
    {
        #region Parser

        enum Token : int
        {
            None = 0,
            ObjectOpen,
            ObjectClose,
            ArrayOpen,
            ArrayClose,
            Colon,
            Comma,
            String,
            Number,
            True,
            False,
            Null,
        }

        static void SkipWhitespace(string jsonString, ref int index)
        {
            for (; index < jsonString.Length; index++)
            {
                switch (jsonString[index])
                {
                    case '\0':
                    case ' ':
                    case '\t':
                    case '\n':
                    case '\r':
                        continue;
                }
                break;
            }
        }

        static Token NextToken(string jsonString, ref int index)
        {
            SkipWhitespace(jsonString, ref index);

            if (index == jsonString.Length)
            {
                return Token.None;
            }

            char c = jsonString[index];
            index++;
            switch (c)
            {
                case '{': return Token.ObjectOpen;

                case '}': return Token.ObjectClose;

                case '[': return Token.ArrayOpen;

                case ']': return Token.ArrayClose;

                case ',': return Token.Comma;

                case '"': return Token.String;

                case '0':
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                case '8':
                case '9':
                case '-': return Token.Number;

                case ':': return Token.Colon;
            }
            index--;

            int remainingLength = jsonString.Length - index;

            // false
            if (remainingLength >= 5)
            {
                if (jsonString[index] == 'f' &&
                    jsonString[index + 1] == 'a' &&
                    jsonString[index + 2] == 'l' &&
                    jsonString[index + 3] == 's' &&
                    jsonString[index + 4] == 'e')
                {
                    index += 5;
                    return Token.False;
                }
            }

            // true
            if (remainingLength >= 4)
            {
                if (jsonString[index] == 't' &&
                    jsonString[index + 1] == 'r' &&
                    jsonString[index + 2] == 'u' &&
                    jsonString[index + 3] == 'e')
                {
                    index += 4;
                    return Token.True;
                }
            }

            // null
            if (remainingLength >= 4)
            {
                if (jsonString[index] == 'n' &&
                    jsonString[index + 1] == 'u' &&
                    jsonString[index + 2] == 'l' &&
                    jsonString[index + 3] == 'l')
                {
                    index += 4;
                    return Token.Null;
                }
            }

            return Token.None;
        }

        static Token PeekToken(string jsonString, int index)
        {
            return NextToken(jsonString, ref index);
        }

        static int LastIndexOfNumber(string jsonString, int index)
        {
            int i = index;
            for (; i < jsonString.Length; i++)
            {
                if ("0123456789+-.eE".IndexOf(jsonString[i]) == -1)
                {
                    break;
                }
            }
            return i - 1;
        }

        static object ParseNumber(string jsonString, ref int index)
        {
            SkipWhitespace(jsonString, ref index);

            int i = LastIndexOfNumber(jsonString, index);
            int len = (i - index) + 1;
            string result = jsonString.Substring(index, len);
            index = i + 1;
            if (long.TryParse(result, NumberStyles.Any, CultureInfo.InvariantCulture, out long l_Long))
            {
                return l_Long;
            }

            if (decimal.TryParse(result, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal l_Decimal))
            {
                return l_Decimal;
            }

            throw new InvalidDataException(string.Format("Cannot parse number {0}", result));
        }

        static string ParseString(string jsonString, ref int index)
        {
            StringBuilder result = new StringBuilder();
            char c;

            SkipWhitespace(jsonString, ref index);

            // "
            c = jsonString[index++];

            while (true)
            {
                if (index >= jsonString.Length)
                {
                    throw new EndOfStreamException(string.Format("Unexpected end of input while reading string at position {0}!", index));
                }

                c = jsonString[index++];
                if (c == '"')
                {
                    break;
                }
                else if (c == '\\')
                {
                    if (index == jsonString.Length)
                    {
                        throw new EndOfStreamException(string.Format("Unexpected end of input while reading string at position {0}!", index));
                    }
                    c = jsonString[index++];
                    if (c == '"')
                    {
                        result.Append('"');
                    }
                    else if (c == '\\')
                    {
                        result.Append('\\');
                    }
                    else if (c == '/')
                    {
                        result.Append('/');
                    }
                    else if (c == 'b')
                    {
                        result.Append('\b');
                    }
                    else if (c == 'f')
                    {
                        result.Append('\f');
                    }
                    else if (c == 'n')
                    {
                        result.Append('\n');
                    }
                    else if (c == 'r')
                    {
                        result.Append('\r');
                    }
                    else if (c == 't')
                    {
                        result.Append('\t');
                    }
                    else if (c == 'u')
                    {
                        int remainingLength = jsonString.Length - index;
                        if (remainingLength >= 4)
                        {
                            // parse the 32 bit hex into an integer codepoint
                            if (!uint.TryParse(jsonString.Substring(index, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint codePoint))
                            {
                                throw new InvalidDataException(string.Format("Error while reading 32bit hex char at position {0}!", index));
                            }
                            if ((codePoint >= 0xD800) && (codePoint <= 0xDFFF))
                            {
                                result.Append((char)codePoint);
                            }
                            else
                            {
                                // convert the integer codepoint to a unicode char and add to string
                                result.Append(char.ConvertFromUtf32((int)codePoint));
                            }
                            // skip 4 chars
                            index += 4;
                        }
                        else
                        {
                            throw new EndOfStreamException(string.Format("Unexpected end of input while reading string at position {0}!", index));
                        }
                    }

                }
                else
                {
                    result.Append(c);
                }

            }
            return result.ToString();
        }

        void ParseObject(JsonNode obj, string jsonString, ref int index)
        {
            Token token;

            // {
            NextToken(jsonString, ref index);

            while (true)
            {
                token = PeekToken(jsonString, index);
                switch (token)
                {
                    case Token.None:
                        //in some cases we need to exit here clean, check!
                        throw new InvalidDataException(string.Format("Missing data at position {0}!", index));

                    case Token.Comma:
                        NextToken(jsonString, ref index);
                        break;

                    case Token.ObjectClose:
                        NextToken(jsonString, ref index);
                        return;

                    default:
                        // name
                        string name = ParseString(jsonString, ref index);
                        // :
                        token = NextToken(jsonString, ref index);
                        if (token != Token.Colon)
                        {
                            throw new InvalidDataException(string.Format("Missing colon in object definition!"));
                        }

                        // value
                        JsonNode sub = new JsonNode(JsonNodeType.Object, name);
                        ParseContent(sub, jsonString, ref index);
                        obj.Add(sub);
                        break;
                }
            }
        }

        void ParseContent(JsonNode obj, string jsonString, ref int index)
        {
            Token token = PeekToken(jsonString, index);
            switch (token)
            {
                case Token.String:
                {
                    string value = ParseString(jsonString, ref index);
                    obj.AddValue(value);
                }
                break;

                case Token.Number:
                {
                    object number = ParseNumber(jsonString, ref index);
                    obj.AddValue(number);
                }
                break;

                case Token.ObjectOpen:
                {
                    ParseObject(obj, jsonString, ref index);
                }
                break;

                case Token.ArrayOpen:
                {
                    ParseArray(obj, jsonString, ref index);
                }
                break;

                case Token.True:
                {
                    NextToken(jsonString, ref index);
                    obj.AddValue(true);
                }
                break;

                case Token.False:
                {
                    NextToken(jsonString, ref index);
                    obj.AddValue(false);
                }
                break;

                case Token.Null:
                {
                    NextToken(jsonString, ref index);
                    obj.AddValue(null);
                }
                break;

                case Token.None: break;

                default: throw new NotImplementedException(string.Format("JsonToken {0} undefined!", token));
            }
        }

        void ParseArray(JsonNode array, string jsonString, ref int index)
        {
            // [
            array.ConvertToArray();
            Token check = NextToken(jsonString, ref index);
            if (check != Token.ArrayOpen)
            {
                throw new InvalidDataException();
            }

            int i = 0;
            while (true)
            {
                Token token = PeekToken(jsonString, index);
                switch (token)
                {
                    case Token.None: return; // throw new InvalidDataException(string.Format("Json object, value or array expected at position {0}!", index));

                    case Token.Comma:
                        NextToken(jsonString, ref index);
                        break;

                    case Token.ArrayClose:
                        NextToken(jsonString, ref index);
                        return;

                    case Token.ObjectOpen:
                        JsonNode sub = new JsonNode(JsonNodeType.Object, (i++).ToString());
                        ParseContent(sub, jsonString, ref index);
                        array.Add(sub);
                        break;

                    default:
                        ParseContent(array, jsonString, ref index);
                        break;
                }
            }
        }

        JsonNode Parse(string jsonString)
        {
            int index = 0;
            Token first = PeekToken(jsonString, index);
            if (first == Token.None)
            {
                throw new InvalidDataException(string.Format("Json data does not start with a valid token!"));
            }
            JsonNode root = new JsonNode(JsonNodeType.Object, "");
            ParseContent(root, jsonString, ref index);
            SkipWhitespace(jsonString, ref index);
            if (index < jsonString.Length)
            {
                throw new InvalidDataException(string.Format("Additional data at end encountered!"));
            }
            //set root to first (single) node if array / object
            if (first == Token.ObjectOpen)
            {
                var subs = root.SubNodes;
                if (subs.Length == 1) return subs[0];
            }
            return root;
        }

        #endregion

        /// <summary>
        /// Loads a whole json file
        /// </summary>
        /// <param name="fileName"></param>
        public JsonReader(string fileName)
        {
            Root = Parse(File.ReadAllText(fileName));
        }

        /// <summary>
        /// Loads Json data from stream
        /// </summary>
        /// <param name="stream"></param>
        public JsonReader(Stream stream)
        {
            Root = Parse(Encoding.UTF8.GetString(stream.ReadAllBytes()));
        }

        /// <summary>
        /// Loads json data
        /// </summary>
        /// <param name="data">Content</param>
        public JsonReader(byte[] data)
        {
            Root = Parse(Encoding.UTF8.GetString(data));
        }

        /// <summary>
        /// Loads json data
        /// </summary>
        /// <param name="data">Content</param>
        public JsonReader(string[] data)
        {
            Root = Parse(string.Join("", data));
        }

        /// <summary>
        /// Obtains the root node
        /// </summary>
        public JsonNode Root { get; private set; }

        /// <summary>
        /// Obtains the child node at the specified path
        /// </summary>
        /// <param name="path">Path of the child node to retrieve</param>
        /// <returns></returns>
        public JsonNode GetNode(params string[] path)
        {
            if (path == null)
            {
                throw new ArgumentNullException("path");
            }

            JsonNode node = Root;
            foreach (string p in path)
            {
                if (node == null)
                {
                    return null;
                }

                node = node[p] as JsonNode;
            }
            return node;
        }

        /// <summary>
        /// Obtains the value at the specified path
        /// </summary>
        /// <param name="path">Path of the value to retrieve</param>
        /// <returns></returns>
        public object GetValue(params string[] path)
        {
            JsonNode obj = GetNode(path);
            if (obj == null)
            {
                return null;
            }

            return obj.Value;
        }

        /// <summary>
        /// Obtains the values at the specified path
        /// </summary>
        /// <param name="path">Path of the values to retrieve</param>
        /// <returns></returns>
        public object[] GetValues(params string[] path)
        {
            JsonNode obj = GetNode(path);
            if (obj == null)
            {
                return null;
            }

            return obj.Values;
        }
    }
}
