using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Cave.Data;
using Cave.IO;

namespace Cave.Web
{
    /// <summary>
    /// Provides Row based serialization
    /// </summary>
    public class JsonSerializer
    {
        /// <summary>
        /// Settings used during de/serialization
        /// </summary>
        [Flags]
        public enum Flags
        {
            /// <summary>No flags</summary>
            None = 0,

            /// <summary>Serialize the layout first, then the data. This adds type safety to the stream but costs a lot of bandwith and time.</summary>
            WithLayout = 1,

            /// <summary>Allows to skip the main object</summary>
            SkipMainObject = 2,
        }

        StringBuilder result = new StringBuilder();
        int Version;
        bool firstitem;

        #region private Data Serializer
        void SerializeRow(RowLayout layout, Row row)
        {
            switch (Version)
            {
                case 1: result.Append("["); break;
                case 2:
                case 3: result.Append("{"); break;
                default: throw new NotImplementedException(string.Format("JSonVersion {0} not implemented!", Version));
            }
            for (int i = 0; i < layout.FieldCount; i++)
            {
                try
                {
                    FieldProperties field = layout.GetProperties(i);
                    string value = field.GetString(row.GetValue(i), "\"", true);
                    if (i > 0)
                    {
                        result.Append(',');
                    }

                    if (Version >= 2)
                    {
                        result.Append($"\"{layout.GetName(i)}\":");
                    }

                    result.Append(value);

                }
                catch (Exception ex)
                {
                    throw new InvalidDataException(string.Format("Error during serialization of layout {0} row {1}", layout, row), ex);
                }
            }
            switch (Version)
            {
                case 1: result.Append("]"); break;
                case 2:
                case 3: result.Append("}"); break;
                default: throw new NotImplementedException(string.Format("JSonVersion {0} not implemented!", Version));
            }
        }

        void SerializeTable(string name, RowLayout layout, long rowCount, IEnumerable<Row> rows)
        {
            if (firstitem)
            {
                firstitem = false;
            }
            else
            {
                result.Append(",\"");
            }
            result.Append(name);
            result.Append("\":{\"Type\":\"Table\",\"RowCount\":");
            result.Append(rowCount);
            if (0 != (Mode & Flags.WithLayout) || Version < 2)
            {
                if (layout.IsTyped)
                {
                    result.Append(",\"RowType\":");
                    result.Append(layout.RowType.FullName.Escape().Box('"'));
                }
                result.Append(",\"Layout\":{\"FieldCount\":");
                result.Append(layout.FieldCount);
                result.Append(",\"Fields\":[");
                int f = 0;
                foreach (FieldProperties field in layout.Fields)
                {
                    if (f++ > 0)
                    {
                        result.Append(',');
                    }

                    result.Append("{\"Name\":\""); result.Append(field.Name);
                    result.Append("\",\"DataType\":\""); result.Append(field.DataType); result.Append("\"");

                    if (field.Flags != 0) { result.Append(",\"Flags\":\""); result.Append(field.Flags); result.Append("\""); }
                    if (field.MaximumLength > 0) { result.Append(",\"MaximumLength\":"); result.Append(field.MaximumLength); }
                    if (field.ValueType != null) { result.Append(",\"ValueType\":\""); result.Append(field.ValueType.Name); result.Append("\""); }
                    if (field.StringEncoding != 0) { result.Append(",\"StringEncoding\":\""); result.Append(field.ValueType.Name); result.Append("\""); }
                    if (field.DataType == DataType.DateTime)
                    {
                        result.Append(",\"DateTimeKind\":\""); result.Append(field.DateTimeKind);
                        result.Append("\",\"DateTimeType\":\""); result.Append(field.DateTimeType); result.Append("\"");
                    }
                    result.Append('}');
                }
                result.Append("]}");
            }
            result.Append(",\"Rows\":[");
            bool first = true;
            foreach (Row row in rows)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    result.Append(",");
                }

                SerializeRow(layout, row);
            }

            switch (Version)
            {
                case 1: result.Append("]}"); break;
                case 2:
                case 3:
                {
                    if (layout.IDFieldIndex < 0)
                    {
                        goto case 1;
                    }
                    //create lookup table
                    result.Append("],\"Lookup\":{");
                    int i = 0;
                    foreach (Row row in rows)
                    {
                        if (i > 0)
                        {
                            result.Append(",");
                        }

                        result.Append($"\"{layout.GetID(row)}\":{i++}");
                    }
                    result.Append("}}");
                    break;
                }
                default: throw new NotImplementedException();
            }
        }
        #endregion

        /// <summary>Initializes a new instance of the <see cref="XmlSerializer"/> class.</summary>
        public JsonSerializer(int version, Flags mode)
        {
            Mode = mode;
            Version = version;
            if (!mode.HasFlag(Flags.SkipMainObject))
            {
                result.Append("{\"CaveJSON\":");
            }
            switch (version)
            {
                case 1:
                case 2:
                    result.Append("{\"Version\":\"");
                    result.Append(version);
                    result.Append(".0\"");
                    break;
                case 3:
                    result.Append("{\"Version\":");
                    result.Append(version);
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(version));
            }
            if (mode.HasFlag(Flags.WithLayout))
            {
                result.Append(",\"Layout\":\"true\"");
            }
        }

        /// <summary>Gets or sets the mode.</summary>
        /// <value>The mode.</value>
        public Flags Mode { get; }

        #region Serializer

        /// <summary>Opens a new sub section.</summary>
        /// <param name="name">The name.</param>
        public void OpenSection(string name)
        {
            if (Version > 2)
            {
                result.Append("{\"" + name + "\":");
                firstitem = true;
            }
        }

        /// <summary>Closes the last opened sub section.</summary>
        public void CloseSection()
        {
            if (Version > 2)
            {
                result.Append("}");
                firstitem = false;
            }
        }

        /// <summary>Serializes the specified items with layout.</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name">The name.</param>
        /// <param name="item">The item.</param>
        public void Serialize<T>(string name, T item) where T : struct
        {
            RowLayout layout = RowLayout.CreateTyped(typeof(T));
            SerializeTable(name, layout, 1, new Row[] { Row.Create(layout, item) });
        }

        /// <summary>Serializes the specified items with layout.</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name">The name.</param>
        /// <param name="items">The items.</param>
        /// <exception cref="System.ArgumentNullException">Items</exception>
        public void Serialize<T>(string name, T[] items) where T : struct
        {
            RowLayout layout = RowLayout.CreateTyped(typeof(T));
            SerializeTable(name, layout, items.Length, items.Select(i => Row.Create(layout, i)));
        }

        /// <summary>Serializes the specified items with layout.</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name">The name.</param>
        /// <param name="items">The items.</param>
        /// <exception cref="System.ArgumentNullException">Items</exception>
        public void Serialize<T>(string name, IList<T> items) where T : struct
        {
            RowLayout layout = RowLayout.CreateTyped(typeof(T));
            SerializeTable(name, layout, items.Count, items.Select(i => Row.Create(layout, i)));
        }

        /// <summary>Serializes the specified table.</summary>
        /// <param name="name">The name.</param>
        /// <param name="table">The table.</param>
        /// <exception cref="System.ArgumentNullException">Items</exception>
        /// <exception cref="ArgumentNullException">Table
        /// or
        /// Writer</exception>
        public void Serialize(string name, ITable table)
        {
            SerializeTable(name, table.Layout, table.RowCount, table.GetRows());
        }

        /// <summary>Serializes the specified table.</summary>
        /// <param name="name">The name.</param>
        /// <param name="layout">The layout.</param>
        /// <param name="rows">The rows.</param>
        /// <exception cref="System.ArgumentNullException">
        /// Rows
        /// or
        /// Layout
        /// </exception>
        /// <exception cref="ArgumentNullException">Rows
        /// or
        /// Layout
        /// or
        /// Writer</exception>
        public void Serialize(string name, RowLayout layout, IList<Row> rows)
        {
            SerializeTable(name, layout, rows.Count, rows);
        }
        #endregion

        /// <summary>Writes the xml to the specified stream.</summary>
        /// <param name="s">The stream to write to.</param>
        public void WriteTo(Stream s)
        {
            DataWriter writer = new DataWriter(s);
            writer.Write(ToString());
        }

        /// <summary>Returns the xml as data.</summary>
        /// <returns></returns>
        public byte[] ToArray()
        {
            return Encoding.UTF8.GetBytes(ToString());
        }

        /// <summary>Returns a <see cref="string" /> that represents this instance.</summary>
        /// <returns>A <see cref="string" /> that represents this instance.</returns>
        public override string ToString()
        {
            if (Mode.HasFlag(Flags.SkipMainObject))
            {
                return result.ToString() + "}";
            }

            return result.ToString() + "}}";
        }
    }
}
