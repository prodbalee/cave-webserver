using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using Cave.Data;

namespace Cave.Web
{
    /// <summary>
    /// Provides Row based serialization for the CaveXML format
    /// </summary>
    public class XmlSerializer
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
        }

        Queue<XElement> path = new Queue<XElement>();

        #region private Data Serializer
        XElement SerializeRow(RowLayout layout, Row row)
        {
            if (layout.IDFieldIndex < 0)
            {
                throw new Exception($"Structure {layout} needs to define an ID field!");
            }

            XElement xRow = new XElement("Row");
            for (int i = 0; i < layout.FieldCount; i++)
            {
                FieldProperties field = layout.GetProperties(i);
                string value = field.GetString(row.GetValue(i), "'", false);
                xRow.SetAttributeValue(field.Name, value);
            }
            return xRow;
        }

        XElement StartSerializeTable(string name, RowLayout layout, long rowCount)
        {
            XElement xtable = new XElement("Table");
            xtable.SetAttributeValue("Name", name);
            xtable.SetAttributeValue("RowCount", rowCount);
            if (layout.IsTyped)
            {
                xtable.Add(new XElement("RowType", layout.RowType.FullName));
            }

            if (0 != (Mode & Flags.WithLayout))
            {
                XElement xlayout = layout.ToXElement();
                xtable.Add(xlayout);
            }
            return xtable;
        }
        #endregion

        /// <summary>Initializes a new instance of the <see cref="XmlSerializer"/> class.</summary>
        public XmlSerializer(int version, Flags mode)
        {
            Mode = mode;
            Root = new XElement("CaveXML");
            Version = version;
            switch (version)
            {
                case 1:
                case 2:
                    Root.SetAttributeValue("Version", "1.1");
                    break;
                case 3:
                    Root.SetAttributeValue("Version", version);
                    break;
                default: throw new NotSupportedException();
            }

            Root.SetAttributeValue("Layout", (0 != (mode & Flags.WithLayout)).ToString());
        }

        /// <summary>The root element</summary>
        public XElement Root;

        /// <summary>Gets or sets the mode.</summary>
        /// <value>The mode.</value>
        public Flags Mode { get; }

        /// <summary>Gets the xml version.</summary>
        /// <value>The xml version.</value>
        public int Version { get; }

        #region Serializer

        /// <summary>Opens a new sub section.</summary>
        /// <param name="name">The name.</param>
        public void OpenSection(string name)
        {
            if (Version > 2)
            {
                XElement newRoot = new XElement(name);
                Root.Add(newRoot);
                path.Enqueue(Root);
                Root = newRoot;
            }
        }

        /// <summary>Closes the last sub section.</summary>
        public void CloseSection()
        {
            if (Version > 2)
            {
                Root = path.Dequeue();
            }
        }

        /// <summary>Serializes the specified items with layout.</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name">The name.</param>
        /// <param name="item">The item.</param>
        public void Serialize<T>(string name, T item) where T : struct
        {
            RowLayout layout = RowLayout.CreateTyped(typeof(T));
            XElement xTable = StartSerializeTable(name, layout, 1);
            XElement xRow = SerializeRow(layout, Row.Create(layout, item));
            xTable.Add(xRow);
            Root.Add(xTable);
        }

        /// <summary>Serializes the specified items with layout.</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name">The name.</param>
        /// <param name="items">The items.</param>
        /// <exception cref="System.ArgumentNullException">Items</exception>
        public void Serialize<T>(string name, T[] items) where T : struct
        {
            if (items == null)
            {
                throw new ArgumentNullException("Items");
            }

            RowLayout layout = RowLayout.CreateTyped(typeof(T));
            XElement xTable = StartSerializeTable(name, layout, items.Length);
            foreach (T item in items)
            {
                XElement xRow = SerializeRow(layout, Row.Create(layout, item));
                xTable.Add(xRow);
            }
            Root.Add(xTable);
        }

        /// <summary>Serializes the specified items with layout.</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name">The name.</param>
        /// <param name="items">The items.</param>
        /// <exception cref="System.ArgumentNullException">Items</exception>
        public void Serialize<T>(string name, IList<T> items) where T : struct
        {
            if (items == null)
            {
                throw new ArgumentNullException("Items");
            }

            RowLayout layout = RowLayout.CreateTyped(typeof(T));
            XElement xTable = StartSerializeTable(name, layout, items.Count);
            foreach (T item in items)
            {
                XElement xRow = SerializeRow(layout, Row.Create(layout, item));
                xTable.Add(xRow);
            }
            Root.Add(xTable);
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
            if (table == null)
            {
                throw new ArgumentNullException("Items");
            }

            XElement xTable = StartSerializeTable(name, table.Layout, table.RowCount);
            foreach (Row row in table.GetRows())
            {
                XElement xRow = SerializeRow(table.Layout, row);
                xTable.Add(xRow);
            }
            Root.Add(xTable);
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
            if (rows == null)
            {
                throw new ArgumentNullException("Rows");
            }

            if (layout == null)
            {
                throw new ArgumentNullException("Layout");
            }

            XElement xTable = StartSerializeTable(name, layout, rows.Count);
            foreach (Row row in rows)
            {
                XElement xRow = SerializeRow(layout, row);
                xTable.Add(xRow);
            }
            Root.Add(xTable);
        }
        #endregion

        /// <summary>Writes the xml to the specified stream.</summary>
        /// <param name="s">The stream to write to.</param>
        public void WriteTo(Stream s)
        {
            s.Write(Root);
        }

        /// <summary>Returns the xml as data.</summary>
        /// <returns></returns>
        public byte[] ToArray()
        {
            using (MemoryStream s = new MemoryStream())
            {
                WriteTo(s);
                return s.ToArray();
            }
        }

        /// <summary>Returns a <see cref="string" /> that represents this instance.</summary>
        /// <returns>A <see cref="string" /> that represents this instance.</returns>
        public override string ToString()
        {
            return Root.ToString();
        }
    }
}