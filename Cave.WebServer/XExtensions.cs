using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Cave.Data;

namespace Cave.Web
{
    /// <summary>
    /// Provides extensions for XElement and XDocument
    /// </summary>
    public static class XExtensions
    {
        /// <summary>Converts FieldProperties to an XElement.</summary>
        /// <param name="field">The field.</param>
        /// <returns></returns>
        public static XElement ToXElement(this FieldProperties field)
        {
            XElement xfield = new XElement("Field");
            xfield.SetAttributeValue("Name", field.Name);
            xfield.SetAttributeValue("DataType", field.DataType);
            if (field.Flags != 0)
            {
                xfield.Add(new XElement("Flags", field.Flags));
            }

            if (field.ValueType != null)
            {
                xfield.Add(new XElement("ValueType", field.ValueType.FullName));
            }

            if (field.StringEncoding != 0)
            {
                xfield.Add(new XElement("StringEncoding", field.StringEncoding));
            }

            if (field.MaximumLength > 0)
            {
                xfield.Add(new XElement("MaximumLength", field.MaximumLength));
            }

            switch (field.DataType)
            {
                case DataType.DateTime:
                    xfield.Add(new XElement("DateTimeType", field.DateTimeType));
                    xfield.Add(new XElement("DateTimeKind", field.DateTimeKind));
                    break;
            }
            return xfield;
        }

        /// <summary>Converts a RowLayout to an XElement.</summary>
        /// <param name="layout">The layout.</param>
        /// <returns></returns>
        public static XElement ToXElement(this RowLayout layout)
        {
            XElement xlayout = new XElement("Layout");
            xlayout.SetAttributeValue("FieldCount", layout.FieldCount);
            foreach (FieldProperties field in layout.Fields)
            {
                xlayout.Add(field.ToXElement());
            }
            return xlayout;
        }

        /// <summary>Gets the default settings.</summary>
        /// <value>The default settings.</value>
        public static XmlWriterSettings DefaultSettings { get; } = new XmlWriterSettings()
        {
            CheckCharacters = true,
            Encoding = Encoding.UTF8,
            Indent = true,
            IndentChars = "\t",
            NewLineChars = "\n",
            NewLineHandling = NewLineHandling.Replace,
            NewLineOnAttributes = true,
#if NET20 || NET35
#else
            NamespaceHandling = NamespaceHandling.OmitDuplicates,
#endif
            CloseOutput = false,
            ConformanceLevel = ConformanceLevel.Document,
            OmitXmlDeclaration = false,
        };

        /// <summary>Writes the specified element.</summary>
        /// <param name="stream">The stream.</param>
        /// <param name="element">The element.</param>
        /// <param name="settings">The settings.</param>
        public static void Write(this Stream stream, XElement element, XmlWriterSettings settings = null)
        {
            if (settings == null)
            {
                settings = DefaultSettings;
            }

            using (XmlWriter writer = XmlWriter.Create(stream, settings))
            {
                element.WriteTo(writer);
            }
        }

        /// <summary>Writes the specified document.</summary>
        /// <param name="stream">The stream.</param>
        /// <param name="doc">The document.</param>
        /// <param name="settings">The settings.</param>
        public static void Write(this Stream stream, XDocument doc, XmlWriterSettings settings = null)
        {
            if (settings == null)
            {
                settings = DefaultSettings;
            }

            using (XmlWriter writer = XmlWriter.Create(stream, settings))
            {
                doc.WriteTo(writer);
            }
        }
    }
}
