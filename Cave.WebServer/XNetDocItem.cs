using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Xml.Linq;

namespace Cave.Web
{
    /// <summary>
    /// Provides a .net documentation item.
    /// </summary>
    public class XNetDocItem
    {
        /// <summary>Gets the summary.</summary>
        /// <value>The summary.</value>
        public string Summary { get; }

        /// <summary>Gets the parameters.</summary>
        /// <value>The parameters.</value>
        public IDictionary<string, string> Parameters { get; }

        /// <summary>Gets the remarks.</summary>
        /// <value>The remarks.</value>
        public string Remarks { get; }

        /// <summary>Gets the return value information.</summary>
        /// <value>The return value information.</value>
        public string Returns { get; }

        /// <summary>Gets the exceptions.</summary>
        /// <value>The exceptions.</value>
        public ReadOnlyCollection<string> Exceptions { get; }

        string GetString(XElement e)
        {
            if (e == null)
            {
                return null;
            }

            string s = e.ToString().ReplaceNewLine(" ");
            int start = s.IndexOf('>') + 1;
            int end = s.LastIndexOf('<');
            if (end > start)
            {
                int len = end - start;
                s = s.Substring(start, len);
                while (s.Contains("  "))
                {
                    s = s.Replace("  ", " ");
                }

                return s.Trim();
            }
            return null;
        }

        /// <summary>Initializes a new instance of the <see cref="XNetDocItem"/> class.</summary>
        /// <param name="element">The element.</param>
        public XNetDocItem(XElement element)
        {
            Summary = GetString(element.Element("summary"));
            Remarks = GetString(element.Element("remarks"));
            Returns = GetString(element.Element("returns"));
            var parameters = new Dictionary<string, string>();
            foreach (XElement e in element.Elements("param"))
            {
                string name = e.Attribute("name").Value;
                parameters.Add(name, GetString(e));
            }
            Parameters = new ReadOnlyDictionary<string, string>(parameters);
            var exceptions = new List<string>();
            foreach (XElement e in element.Elements("exception").Descendants())
            {
                XNode node = e;
                while (node != null)
                {
                    string s = node.ToString();
                    node = node.NextNode;
                    foreach (string line in s.SplitNewLine())
                    {
                        s = line.Trim();
                        if (s.Length == 0 || s == "or")
                        {
                            continue;
                        }

                        exceptions.Add(s);
                    }
                }
                break;
            }
            Exceptions = new ReadOnlyCollection<string>(exceptions);
        }
    }
}
