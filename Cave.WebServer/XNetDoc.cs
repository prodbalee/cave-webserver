using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Xml.Linq;

namespace Cave.Web
{
    /// <summary>
    /// Provides a .net documentation
    /// </summary>
    public class XNetDoc
    {
        /// <summary>Loads all documentation xml files from the program path</summary>
        /// <returns></returns>
        public static XNetDoc FromProgramPath()
        {
            XNetDoc result = new XNetDoc();
            foreach (string file in Directory.GetFiles(FileSystem.ProgramDirectory, "*.*"))
            {
                if (Path.GetExtension(file).ToLower() == ".xml")
                {
                    try { result.Load(file); }
                    catch (Exception ex) { Trace.TraceError("Could not load <red>{0}\n{1}", file, ex); }
                }
            }
            return result;
        }

        /// <summary>The methods</summary>
        public readonly Dictionary<string, XNetDocItem> Methods = new Dictionary<string, XNetDocItem>();

        /// <summary>The fields</summary>
        public readonly Dictionary<string, XNetDocItem> Fields = new Dictionary<string, XNetDocItem>();

        /// <summary>The types</summary>
        public readonly Dictionary<string, XNetDocItem> Types = new Dictionary<string, XNetDocItem>();

        /// <summary>The properties</summary>
        public readonly Dictionary<string, XNetDocItem> Properties = new Dictionary<string, XNetDocItem>();

        /// <summary>The enums</summary>
        public readonly Dictionary<string, XNetDocItem> Enums = new Dictionary<string, XNetDocItem>();

        /// <summary>Initializes a new instance of the <see cref="XNetDoc"/> class.</summary>
        public XNetDoc() { }

        /// <summary>Loads the specified filename.</summary>
        /// <param name="filename">The filename.</param>
        public void Load(string filename)
        {
            XDocument xDoc = XDocument.Load(filename);
            IEnumerable<XElement> xMembers = xDoc.Root.Elements("members").Elements("member");

            foreach (XElement xMember in xMembers)
            {
                string name = xMember.Attribute("name").Value;
                switch (name.Substring(0, 2))
                {
                    case "M:":
                    {
                        int start = name.IndexOf('(');
                        if (start < 0)
                        {
                            break;
                        }

                        if (!name.Substring(start).Contains("eWebData"))
                        {
                            break;
                        }

                        name = name.Substring(2, start - 2);
                        if (name.Contains("#ctor"))
                        {
                            break;
                        }

                        Methods.Add(name, new XNetDocItem(xMember));
                        break;
                    }
                    case "F:": Fields.Add(name.Substring(2), new XNetDocItem(xMember)); break;
                    case "T:": Types.Add(name.Substring(2), new XNetDocItem(xMember)); break;
                    case "P:": Properties.Add(name.Substring(2), new XNetDocItem(xMember)); break;
                    case "E:": Enums.Add(name.Substring(2), new XNetDocItem(xMember)); break;
                    default: break;
                }
            }
        }

        /// <summary>Gets an enum documentation.</summary>
        /// <param name="type">The type.</param>
        /// <returns></returns>
        public XNetDocItem GetEnum(Type type)
        {
            Types.TryGetValue(type.FullName, out XNetDocItem item);
            return item;
        }

        /// <summary>Gets a method documentation.</summary>
        /// <param name="method">The method.</param>
        /// <returns></returns>
        public XNetDocItem GetMethod(MethodInfo method)
        {
            //string fullname = string.Format("{0}.{1}({2})", method.ReflectedType.FullName, method.Name, string.Join(",", method.GetParameters().Select(o => o.ParameterType.ToString()).ToArray()));
            string fullname = string.Format("{0}.{1}", method.ReflectedType.FullName.BeforeFirst('['), method.Name);
            Methods.TryGetValue(fullname, out XNetDocItem item);
            return item;
        }

        /// <summary>Gets a type documentation.</summary>
        /// <param name="type">The type.</param>
        /// <returns></returns>
        public XNetDocItem GetType(Type type)
        {
            Types.TryGetValue(type.FullName, out XNetDocItem item);
            return item;
        }

        /// <summary>Gets a field documentation.</summary>
        /// <param name="type">The type.</param>
        /// <param name="fieldName">Name of the field.</param>
        /// <returns></returns>
        public XNetDocItem GetField(Type type, string fieldName)
        {
            Fields.TryGetValue(type.FullName + "." + fieldName, out XNetDocItem item);
            return item;
        }
    }
}
