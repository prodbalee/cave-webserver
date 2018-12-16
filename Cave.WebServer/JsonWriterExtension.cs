using System.Collections;
using Cave.IO;

namespace Cave.Data
{
    /// <summary>
    /// Extensions to the <see cref="JsonWriter"/> class.
    /// </summary>
    public static class JsonWriterExtension
    {
        /// <summary>Writes an array.</summary>
        /// <param name="writer">The writer.</param>
        /// <param name="name">The name of the array.</param>
        /// <param name="field">The field properties.</param>
        /// <param name="values">The values.</param>
        public static void Array(this JsonWriter writer, string name, FieldProperties field, IEnumerable values)
        {
            writer.BeginArray(name);
            foreach (object val in values)
            {
                string value = field.GetString(val, "\"", true);
                writer.ArrayValue(value, false, false);
            }
            writer.EndArray();
        }
    }
}
