using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using Cave.Data;
using Cave.IO;

namespace Cave.Web
{
    /// <summary>
    /// Provides a deserializer for CaveXML.
    /// </summary>
    public class XmlDeserializer
    {
        Dictionary<string, ITable> m_Tables;
        string m_StringMarker;

        void InternalParse(XDocument doc)
        {
            if (m_Tables != null)
            {
                throw new InvalidOperationException("Parse was already called!");
            }

            m_Tables = new Dictionary<string, ITable>();

            XElement root = doc.Root;
            if (root.Name.LocalName != "CaveXML")
            {
                throw new InvalidDataException("CaveXML root missing!");
            }

            if (int.TryParse(root.Attribute("Version").Value, out int verInt))
            {
                Version = verInt;
            }
            else
            {
                Version ver = new Version(root.Attribute("Version").Value);
                if (ver.ToString() == "1.0")
                {
                    Version = 1;
                }
                else
                {
                    Version = 2;
                }
            }

            switch (Version)
            {
                case 1: m_StringMarker = null; break;
                case 2:
                case 3: m_StringMarker = "'"; break;
                default: throw new Exception("Invalid CaveXML Version!");
            }

            foreach (XElement element in root.Elements())
            {
                switch (element.Name.LocalName)
                {
                    case "Table":
                        ITable table = ParseTable(element);
                        m_Tables.Add(table.Name, table);
                        break;
                    default: throw new InvalidDataException(string.Format("Unknown tree type {0}", element.Name.LocalName));
                }
            }
        }

        ITable ParseTable(XElement xTable)
        {
            string name = xTable.Attribute("Name").Value;
            int rowCount = int.Parse(xTable.Attribute("RowCount").Value);
            RowLayout typedLayout = null;
            RowLayout layout = null;
            ITable table = null;

            foreach (XElement element in xTable.Elements())
            {
                switch (element.Name.LocalName)
                {
                    case "RowType":
                        if (typedLayout != null)
                        {
                            throw new InvalidDataException(string.Format("RowType may not be present multiple times at xTable {0}", name));
                        }

                        Type type = LoadType(element.Value, AppDom.LoadMode.NoException);
                        if (type == null)
                        {
                            Trace.TraceWarning("RowType {0} cannot be loaded!", element.Value);
                        }
                        else
                        {
                            typedLayout = RowLayout.CreateTyped(type, name);
                        }
                        if (layout != null && typedLayout != null)
                        {
                            RowLayout.CheckLayout(typedLayout, layout);
                        }

                        break;
                    case "Layout":
                        if (layout != null)
                        {
                            throw new InvalidDataException(string.Format("Layout may not be present multiple times at xTable {0}", name));
                        }

                        layout = ParseLayout(name, element);
                        if (layout != null && typedLayout != null)
                        {
                            RowLayout.CheckLayout(typedLayout, layout);
                        }

                        break;
                    case "Row":
                        if (table == null)
                        {
                            if (layout == null && typedLayout == null)
                            {
                                throw new InvalidDataException("At least one of Layout or RowType need to precede rows!");
                            }

                            table = new MemoryTable(typedLayout ?? layout);
                        }
                        ParseRow(table, element);
                        break;
                    default: throw new InvalidDataException(string.Format("Unknown tree type {0}", element.Name.LocalName));
                }
            }

            if (table == null)
            {
                if (layout == null && typedLayout == null)
                {
                    throw new InvalidDataException("Tables without Layout, RowType and rows are not supported!");
                }

                table = new MemoryTable(typedLayout ?? layout);
            }
            return table;
        }

        void ParseRow(ITable table, XElement xRow)
        {
            object[] row = new object[table.Layout.FieldCount];
            foreach (XAttribute field in xRow.Attributes())
            {
                int i = table.Layout.GetFieldIndex(field.Name.LocalName);
                row[i] = table.Layout.ParseValue(i, field.Value, m_StringMarker, CultureInfo.InvariantCulture);
            }
            table.Insert(new Row(row));
        }

        RowLayout ParseLayout(string tableName, XElement xLayout)
        {
            List<FieldProperties> fields = new List<FieldProperties>();
            int fieldCount = int.Parse(xLayout.Attribute("FieldCount").Value);

            foreach (XElement element in xLayout.Elements())
            {
                switch (element.Name.LocalName)
                {
                    case "Field":
                        FieldProperties field = ParseField(tableName, element);
                        fields.Add(field);
                        break;
                    default: throw new InvalidDataException(string.Format("Unknown tree type {0}", element.Name.LocalName));
                }
            }

            if (fields.Count != fieldCount)
            {
                throw new InvalidDataException();
            }

            return RowLayout.CreateUntyped(tableName, fields.ToArray());
        }

        FieldProperties ParseField(string tableName, XElement xField)
        {
            string name = xField.Attribute("Name").Value;
            DataType dataType = (DataType)Enum.Parse(typeof(DataType), xField.Attribute("DataType").Value);
            FieldFlags flags = FieldFlags.None;
            Type valueType = null;
            StringEncoding stringEncoding = StringEncoding.Undefined;
            int maxLength = 0;
            DateTimeType dateTimeType = DateTimeType.Native;
            DateTimeKind dateTimeKind = DateTimeKind.Unspecified;
            string description = null;
            foreach (XElement element in xField.Elements())
            {
                switch (element.Name.LocalName)
                {
                    case "Flags": flags = (FieldFlags)Enum.Parse(typeof(FieldFlags), element.Value); break;
                    case "ValueType": valueType = LoadType(element.Value); break;
                    case "StringEncoding": stringEncoding = (StringEncoding)Enum.Parse(typeof(StringEncoding), element.Value); break;
                    case "MaximumLength": maxLength = int.Parse(element.Value); break;
                    case "DateTimeType": dateTimeType = (DateTimeType)Enum.Parse(typeof(DateTimeType), element.Value); break;
                    case "DateTimeKind": dateTimeKind = (DateTimeKind)Enum.Parse(typeof(DateTimeKind), element.Value); break;
                    case "Description": description = element.Value; break;
                    default: throw new InvalidDataException(string.Format("Unknown tree type {0}", element.Name.LocalName));
                }
            }
            return new FieldProperties(tableName, flags, dataType, valueType, maxLength, name, dataType, dateTimeType, dateTimeKind, stringEncoding, name, description, null, null);
        }

        Type LoadType(string typeName, AppDom.LoadMode mode = AppDom.LoadMode.None)
        {
            // typeload fix (old version)
            if (typeName.StartsWith("Cave.Web.Auth."))
            {
                typeName = typeName.Replace("Cave.Web.Auth.", "Cave.Auth.");
            }            

            return AppDom.FindType(typeName, mode);
        }

        /// <summary>Parses the specified stream.</summary>
        /// <param name="data">The data.</param>
        public void Parse(byte[] data)
        {
            using (MemoryStream ms = new MemoryStream(data))
            {
                Parse(ms);
            }
        }

        /// <summary>Parses the specified stream.</summary>
        /// <param name="stream">The stream.</param>
        public void Parse(Stream stream)
        {
            using (StreamReader sr = new StreamReader(stream))
            {
                Parse(XDocument.Load(sr));
            }
        }

        /// <summary>Parses the specified document.</summary>
        /// <param name="doc">The document.</param>
        /// <exception cref="System.Exception">Malformed CaveXML. Mandatory attribute could not be found!</exception>
        public void Parse(XDocument doc)
        {
            try
            {
                InternalParse(doc);
            }
            catch (ArgumentNullException)
            {
                throw new Exception("Malformed CaveXML. Mandatory attribute could not be found!");
            }
        }

        /// <summary>Gets the message.</summary>
        /// <value>The message.</value>
        public WebMessage Message => GetRow<WebMessage>("Result");

        /// <summary>Gets the version.</summary>
        /// <value>The version.</value>
        public int Version { get; private set; }

        /// <summary>Gets the table names.</summary>
        /// <value>The table names.</value>
        public ICollection<string> TableNames => m_Tables.Keys;

        /// <summary>Gets the tables.</summary>
        /// <value>The tables.</value>
        public ICollection<ITable> Tables => m_Tables.Values;

        /// <summary>Gets a row result from the specified table.</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="tableName">Name of the table.</param>
        /// <param name="throwError">Throw an exception on multiple results</param>
        /// <returns></returns>
        /// <exception cref="InvalidDataException">
        /// </exception>
        /// <remarks>
        /// This can only be used to get single row results! For multi row results use
        /// <see cref="GetTable(string)" />, <see cref="GetTable{T}(string)" />
        /// or the <see cref="Tables" /> property.
        /// </remarks>
        public T GetRow<T>(string tableName = null, bool throwError = false) where T : struct
        {
            if (tableName == null)
            {
                tableName = TableAttribute.GetName(typeof(T));
            }

            T? value = null;
            ITable table = GetTable(tableName);
            foreach (Row row in table.GetRows())
            {
                if (throwError && value != null)
                {
                    throw new InvalidDataException(string.Format("Expected single result but got multiple datasets at table {0}!", tableName));
                }
                value = row.GetStruct<T>(RowLayout.CreateTyped(typeof(T)));
            }
            if (value == null)
            {
                if (throwError)
                {
                    throw new InvalidDataException(string.Format("No dataset at table {0}", tableName));
                }
                return new T();
            }
            return value.Value;
        }

        /// <summary>Determines whether the specified table is present.</summary>
        /// <param name="tableName">Name of the table.</param>
        /// <returns><c>true</c> if the specified table is present; otherwise, <c>false</c>.</returns>
        public bool HasTable(string tableName)
        {
            return m_Tables.ContainsKey(tableName);
        }

        /// <summary>Gets the table with the specified name.</summary>
        /// <param name="tableName">Name of the table.</param>
        /// <returns></returns>
        public ITable GetTable(string tableName)
        {
            return m_Tables[tableName];
        }

        /// <summary>Gets the table with the specified name.</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name">The name.</param>
        /// <returns></returns>
        public ITable<T> GetTable<T>(string name = null) where T : struct
        {
            MemoryTable<T> result = new MemoryTable<T>();
            if (name == null)
            {
                name = result.Name;
            }

            if (HasTable(name))
            {
                result.LoadTable(GetTable(name));
            }

            return result;
        }
    }
}