using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Cave.IO
{
    /// <summary>
    /// Privides a simple json writer
    /// </summary>
    public class JsonWriter
    {
        StringBuilder sb;
        Stack<bool> subIsArray = new Stack<bool>();
        Stack<bool> isFirstItem = new Stack<bool>();
        bool firstItem;
        bool hasRootObject;

        /// <summary>Initializes a new instance of the <see cref="JsonWriter"/> class.</summary>
        public JsonWriter()
        {
            sb = new StringBuilder();
            firstItem = true;
        }

        /// <summary>Begins an object.</summary>
        public void BeginObject(string name = null)
        {
            if (sb.Length == 0)
            {
                hasRootObject = true;
                sb.Append("{");
            }
            if (firstItem) { firstItem = false; } else { sb.Append(","); }
            if (!string.IsNullOrEmpty(name))
            {
                sb.Append(name.Box('"'));
                sb.Append(":");
            }
            sb.Append("{");
            isFirstItem.Push(firstItem);
            firstItem = true;
            subIsArray.Push(false);
        }

        /// <summary>Ends an object.</summary>
        public void EndObject()
        {
            if (subIsArray.Count == 0) { throw new InvalidOperationException("Unexpected EndObject()."); }
            if (subIsArray.Peek()) { throw new InvalidOperationException("EndArray() expected!"); }

            subIsArray.Pop();
            firstItem = isFirstItem.Pop();
            sb.Append("}");
        }

        /// <summary>Begins an array.</summary>
        public void BeginArray(string name = null)
        {
            if (!string.IsNullOrEmpty(name))
            {
                if (sb.Length == 0)
                {
                    hasRootObject = true;
                    sb.Append("{");
                }
                if (firstItem) { firstItem = false; } else { sb.Append(","); }
                sb.Append(name.Box('"'));
                sb.Append(":");
            }
            sb.Append("[");
            isFirstItem.Push(firstItem);
            firstItem = true;
            subIsArray.Push(true);
        }

        /// <summary>Ends an array.</summary>
        /// <exception cref="InvalidOperationException"></exception>
        public void EndArray()
        {
            if (subIsArray.Count == 0) { throw new InvalidOperationException("Unexpected EndObject()."); }
            if (!subIsArray.Peek()) { throw new InvalidOperationException("EndObject() expected!"); }
            subIsArray.Pop();
            firstItem = isFirstItem.Pop();
            sb.Append("]");
        }

        /// <summary>Writes an object</summary>
        /// <param name="name">The name.</param>
        /// <param name="value">The value.</param>
        /// <param name="box">if set to <c>true</c> [box].</param>
        /// <param name="escape">if set to <c>true</c> [escape].</param>
        public void Object(string name, string value, bool box, bool escape)
        {
            if (sb.Length == 0)
            {
                hasRootObject = true;
                sb.Append("{");
            }
            if (firstItem) { firstItem = false; } else { sb.Append(","); }
            //escape value
            if (escape) { value = value.Escape(); }
            if (box) { value = value.Box('"'); }
            sb.Append($"\"{name}\":{value}");
        }

        /// <summary>Writes an object</summary>
        /// <param name="name">The name.</param>
        /// <param name="value">The value.</param>
        public void Object(string name, bool value)
        {
            String(name, value.ToString().ToLower());
        }

        /// <summary>Writes an object</summary>
        /// <param name="name">The name.</param>
        /// <param name="value">The value.</param>
        public void Object(string name, int value)
        {
            Object(name, value.ToString(), false, false);
        }

        /// <summary>Writes an object</summary>
        /// <param name="name">The name.</param>
        /// <param name="value">The value.</param>
        public void Object(string name, long value)
        {
            Object(name, value.ToString(), false, false);
        }

        /// <summary>Writes an object</summary>
        /// <param name="name">The name.</param>
        /// <param name="value">The value.</param>
        public void Object(string name, double value)
        {
            Object(name, value.ToString("R", CultureInfo.InvariantCulture), false, false);
        }

        /// <summary>Writes an object</summary>
        /// <param name="name">The name.</param>
        /// <param name="value">The value.</param>
        public void Object(string name, float value)
        {
            Object(name, value.ToString("R", CultureInfo.InvariantCulture), false, false);
        }

        /// <summary>Writes an object</summary>
        /// <param name="name">The name.</param>
        /// <param name="value">The value.</param>
        public void String(string name, string value)
        {
            Object(name, value, true, true);
        }

        /// <summary>Arrays the specified value.</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name">The name of the array, may be null.</param>
        /// <param name="array">The array.</param>
        /// <param name="box">if set to <c>true</c> [box].</param>
        /// <param name="escape">if set to <c>true</c> [escape].</param>
        public void Array<T>(string name, IEnumerable<T> array, bool box, bool escape)
        {
            BeginArray(name);
            foreach (T value in array)
            {
                ArrayValue(value.ToString(), box, escape);
            }
            EndArray();
        }

        /// <summary>Writes a value</summary>
        /// <param name="value">The value.</param>
        /// <param name="box">box the string</param>
        /// <param name="escape">if set to <c>true</c> [escape].</param>
        public void ArrayValue(string value, bool box, bool escape)
        {
            if (firstItem) { firstItem = false; } else { sb.Append(","); }
            if (escape) { value = value.Escape(); }
            if (box) { value = value.Box('"'); }
            sb.Append(value);
        }

        /// <summary>Writes a value</summary>
        /// <param name="value">The value.</param>
        public void ArrayValue(bool value)
        {
            ArrayValue(value.ToString().ToLower(), true, false);
        }

        /// <summary>Writes a value</summary>
        /// <param name="value">The value.</param>
        public void ArrayValue(int value)
        {
            ArrayValue(value.ToString(), false, false);
        }

        /// <summary>Writes a value</summary>
        /// <param name="value">The value.</param>
        public void ArrayValue(long value)
        {
            ArrayValue(value.ToString(), false, false);
        }

        /// <summary>Writes a value</summary>
        /// <param name="value">The value.</param>
        public void ArrayValue(float value)
        {
            ArrayValue(value.ToString("R", CultureInfo.InvariantCulture), false, false);
        }

        /// <summary>Writes a value</summary>
        /// <param name="value">The value.</param>
        public void ArrayValue(double value)
        {
            ArrayValue(value.ToString("R", CultureInfo.InvariantCulture), false, false);
        }

        /// <summary>Returns a <see cref="System.String" /> that represents this instance.</summary>
        /// <returns>A <see cref="System.String" /> that represents this instance.</returns>
        public override string ToString()
        {
            if (subIsArray.Count != 0) { throw new Exception("Not completed!"); }
            if (hasRootObject) { return sb.ToString() + "}"; }
            return sb.ToString();
        }
    }
}
