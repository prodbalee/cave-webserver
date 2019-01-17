using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Cave.Data;
using Cave.IO;

namespace Cave.Web
{
    /// <summary>
    /// Provides automatic documentation for rpc functions
    /// </summary>
    public class WebExplain
    {
        XNetDoc documentation;

        /// <summary>Initializes a new instance of the <see cref="WebExplain"/> class.</summary>
        public WebExplain()
        {
            documentation = XNetDoc.FromProgramPath();
            //we dont need properties
            documentation.Properties.Clear();
        }

        /// <summary>Gets the name of the log source.</summary>
        /// <value>The name of the log source.</value>
        public string LogSourceName => "CaveWebExplain";

        /// <summary>Entry point for explain.</summary>
        /// <param name="data">The data.</param>
        /// <exception cref="WebServerException">Error during explain.</exception>
        public void Explain(WebData data)
        {
            string name = null, type = null;
            try
            {
                if (data.Request.Parameters.Count > 0)
                {
                    if (data.Request.Parameters.TryGetValue("function", out name))
                    {
                        type = "function";
                        ExplainFunction(data, name);
                        return;
                    }
                    if (data.Request.Parameters.TryGetValue("type", out name))
                    {
                        type = "type";
                        ExplainType(data, name);
                        return;
                    }
                }
                ExplainFunctionList(data);
            }
            catch (Exception ex)
            {
                if (ex is WebServerException)
                {
                    throw;
                }

                Trace.TraceError("Error during explain <cyan>{0} <magenta>{1}.", type, name);
                throw new WebServerException(WebError.InternalServerError, 0, "Error during explain {0} {1}.", type, name);
            }
        }

        void ExplainFunction(WebData data, string name)
        {
            if (!data.Server.RegisteredPaths.TryGetValue(name, out WebServerMethod function))
            {
                throw new WebServerException(WebError.InvalidParameters, 0, "Unknown function or function not registered!");
            }
            ExplainFunction(data, function);
        }

        void ExplainFunction(WebData data, WebServerMethod function)
        {
            HtmlPageBuilder html = new HtmlPageBuilder(data.Request);
            {
                string path = "";
                string[] parts = function.FullPaths.First().Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                int last = parts.Length - 1;
                for (int n = 0; n < parts.Length; n++)
                {
                    path += "/" + parts[n];
                    html.Breadcrump.Add(new WebLink() { Text = parts[n], Link = (n != last) ? $"/Explain?functions={path}" : $"/Explain?function={path}" });
                }
            }

            Bootstrap4 content = html.Content;
            WebServerAuthType authType = function.PageAttribute?.AuthType ?? WebServerAuthType.None;
            {
                string link = function.FullPaths.First();
                Bootstrap4 head = new Bootstrap4();
                if (authType != WebServerAuthType.None)
                {
                    //head.DivOpen(Bootstrap4.Item.float_right);
                    head.DivOpen(Bootstrap4.Item.float_right);
                    AddBadges(head, function.PageAttribute);
                    head.DivClose(Bootstrap4.Item.float_right);
                    //head.AddHtml("<br/>");
                }
                head.AddHtml("<h2>");
                head.AddHtml(function.Method.Name.SplitCamelCase().Join("&nbsp;"));
                if (function.Parameters.Length > 0)
                {
                    head.AddHtml(" (");
                    head.AddHtml(function.ParameterString());
                    head.AddHtml(")");
                }
                head.AddHtml("</h2>");

                head.DivOpen(Bootstrap4.Item.float_right);
                head.Link("html", link + ".html", "btn btn-sm btn-outline-primary");
                head.Link("json", link + ".json", "btn btn-sm btn-outline-primary");
                head.Link("xml", link + ".xml", "btn btn-sm btn-outline-primary");
                head.Link("plain", link + ".txt", "btn btn-sm btn-outline-primary");
                head.DivClose(Bootstrap4.Item.float_right);

                head.AddHtml(function.Method.DeclaringType.AssemblyQualifiedName);
                content.CardOpen(head.ToString());
            }
            XNetDocItem doc = documentation.GetMethod(function.Method);
            DocumentHtml(content, doc, function.IsAction ? "Generic action" : function.Method.ToString());
            content.ListGroupOpen();
            int i = 0;
            foreach (ParameterInfo parameter in function.Parameters)
            {
                if (parameter.ParameterType == typeof(WebData))
                {
                    continue;
                }

                ParameterHtml(content, i++, parameter, doc);
            }
            content.ListGroupClose();
            content.CardClose();
            content.AddHtml("&nbsp;");
            WebMessage message = WebMessage.Create("Explain " + function.Name, string.Format("Explain function {0}", function));
            data.Answer = html.ToAnswer(message);
        }

        void AddBadges(Bootstrap4 bootstrap, WebPageAttribute pageAttribute)
        {
            string badgeType;
            switch (pageAttribute.AuthType)
            {
                case WebServerAuthType.Basic: badgeType = "badge-danger"; break;
                case WebServerAuthType.Session: badgeType = "badge-primary"; break;
                default: badgeType = "badge-default"; break;
            }
            bootstrap.AddHtml(Bootstrap4.GetBadge(pageAttribute.AuthType.ToString(), badgeType));
            if (pageAttribute.AuthData != null)
            {
                bootstrap.AddHtml(" ");
                bootstrap.AddHtml(Bootstrap4.GetBadge(pageAttribute.AuthData, "badge-info"));
            }
        }

        void ExplainType(WebData data, string name)
        {
            Type t = AppDom.FindType(name, AppDom.LoadMode.NoException);
            if (t == null)
            {
                throw new WebServerException(WebError.InvalidParameters, 0, string.Format("Type {0} is unknown!", name));
            }

            if (t != null)
            {
                if (t.IsEnum)
                {
                    ExplainEnum(data, t);
                    return;
                }
                if (!t.IsPrimitive && t.IsValueType) //struct
                {
                    ExplainStruct(data, t);
                    return;
                }
            }
            throw new WebServerException(WebError.InvalidOperation, 0, string.Format("Type {0} cannot be explained!", name));
        }

        void ExplainEnum(WebData data, Type t)
        {
            HtmlPageBuilder html = new HtmlPageBuilder(data.Request);
            html.Breadcrump.Add(new WebLink() { Text = t.FullName });
            Bootstrap4 content = html.Content;
            content.CardOpenText($"Enum {t.Name}");
            DocumentHtml(content, documentation.GetEnum(t), t.ToString());
            content.ListGroupOpen();
            int i = 0;
            foreach (object value in Enum.GetValues(t))
            {
                XNetDocItem doc = documentation.GetField(t, value.ToString());
                FieldHtml(content, i++, value, doc);
            }
            content.ListGroupClose();
            content.CardClose();
            content.AddHtml("&nbsp;");
            WebMessage message = WebMessage.Create("Explain " + t.Name, string.Format("Explain enum {0}", t.Name));
            data.Answer = html.ToAnswer(message);
        }

        void ExplainStruct(WebData data, Type t)
        {
            HtmlPageBuilder html = new HtmlPageBuilder(data.Request);
            html.Breadcrump.Add(new WebLink() { Text = t.FullName });
            Bootstrap4 content = html.Content;
            RowLayout layout = RowLayout.CreateTyped(t);
            content.CardOpen($"<h2>Struct {t.Name}<h2><h4>Table {layout.Name}</h4>{layout.FieldCount} Fields, {t.AssemblyQualifiedName}");

            DocumentHtml(content, documentation.GetType(t), t.ToString());
            content.ListGroupOpen();
            int i = 0;
            foreach (FieldProperties field in layout.Fields)
            {
                XNetDocItem doc = documentation.GetField(t, field.Name.ToString());
                FieldHtml(content, i++, field, doc);
            }
            content.ListGroupClose();
            content.CardClose();
            content.AddHtml("&nbsp;");
            WebMessage message = WebMessage.Create("Explain " + t.Name, string.Format("Explain struct {0}", t.Name));
            data.Answer = html.ToAnswer(message);
        }

        void Paragraph(string className, Bootstrap4 content, string data, string textType = null)
        {
            if (data == null)
            {
                return;
            }

            if (textType != null)
            {
                className = ($"{className} " + textType == null ? "" : "text-" + textType).Trim();
            }

            foreach (XNode n in XElement.Parse($"<data>{data}</data>").Nodes())
            {
                string s = n.ToString();
                if (s.StartsWith("<see"))
                {
                    s = s.GetString(-1, ":", "/>").TrimEnd(' ', '"');
                    bool done = false;
                    try
                    {
                        Type t = AppDom.FindType(s, AppDom.LoadMode.NoException);
                        if (t != null && t.IsValueType && !t.IsEnum && !t.IsPrimitive)
                        {
                            RowLayout layout = RowLayout.CreateTyped(t);
                            content.Link($"Table {layout.Name}", "/Explain?type=" + s, textType == null ? null : "btn btn-sm btn-outline-" + textType);
                            done = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceError("Error explaining type {0}. {1} Falling back to simple description.", s, ex);
                    }
                    if (!done)
                    {
                        content.Link(s.AfterLast('.'), "/Explain?type=" + s, textType == null ? null : "btn btn-sm btn-outline-" + textType);
                    }
                    continue;
                }
                content.AddHtml(s);
            }
        }

        void DocumentHtml(Bootstrap4 content, XNetDocItem xNetDocItem, string defaultSummary)
        {
            string summary = (xNetDocItem?.Summary != null) ? xNetDocItem.Summary : defaultSummary;
            if (summary != null)
            {
                int cdata = summary.IndexOf("<![CDATA[");
                if (cdata > -1)
                {
                    int cdataEnd = summary.IndexOf("]]>", cdata + 9);
                    string code = summary.Substring(cdata + 9, cdataEnd - cdata - 9);
                    Paragraph(null, content, summary.Substring(0, cdata), "primary");
                    Paragraph(null, content, "<code>" + code + "</code>", "code");
                    summary = summary.Substring(cdataEnd + 3);
                    if (summary.Trim().Length > 0)
                    {
                        Paragraph(null, content, summary, "primary");
                    }
                }
                else
                {
                    Paragraph(null, content, summary, "primary");
                }
            }
            if (xNetDocItem == null)
            {
                return;
            }

            if (xNetDocItem.Remarks != null)
            {
                Paragraph(null, content, xNetDocItem.Remarks, "info");
            }

            if (xNetDocItem.Returns != null)
            {
                Paragraph(null, content, xNetDocItem.Returns, "success");
            }

            if (xNetDocItem.Exceptions != null)
            {
                content.ParagraphOpen("text-danger");
                bool first = true;
                foreach (string line in xNetDocItem.Exceptions)
                {
                    if (line.StartsWith("<see"))
                    {
                        if (first)
                        {
                            first = false;
                        }
                        else
                        {
                            content.AddHtml("<br />");
                        }

                        string code = line.GetString(-1, "CaveWebError", "\"", false);
                        if (code == null)
                        {
                            content.Text(line);
                        }
                        else
                        {
                            content.AddHtml($"<strong>{Bootstrap4.GetLink("CaveWebError" + code, "/Explain?type=Cave.Web.CaveWebError", "text-danger")}</strong>: ");
                        }
                    }
                    else
                    {
                        content.Text(line);
                    }
                }
                content.ParagraphClose();
            }
        }

        void ExplainFunctionList(WebData data)
        {
            HtmlPageBuilder html = new HtmlPageBuilder(data.Request);

            IEnumerable<KeyValuePair<string, WebServerMethod>> paths = data.Server.RegisteredPaths;

            if (data.Request.Parameters.TryGetValue("functions", out string functions))
            {
                string path = "";
                string[] parts = functions.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                for (int n = 0; n < parts.Length; n++)
                {
                    path += "/" + parts[n];
                    html.Breadcrump.Add(new WebLink() { Text = parts[n], Link = $"/Explain?functions={path}" });
                }
                paths = paths.Where(p => p.Key.StartsWith(functions));
            }

            Bootstrap4 content = html.Content;
            content.ListGroupOpen();
            int i = 0;

            ILookup<WebServerMethod, string> lookup = paths.ToLookup(p => p.Value, p => p.Key);
            foreach (IGrouping<WebServerMethod, string> item in lookup)
            {
                WebServerMethod function = item.Key;

                //if (item.Key == "/") continue;
                content.ListGroupItemOpen((0 == i++ % 2 ? " list-group-item-info" : null));
                XNetDocItem doc = documentation.GetMethod(function.Method);

                content.AddHtml("<div style=\"width:100%\">");
                content.DivOpen(Bootstrap4.Item.row);
                WebServerAuthType authType = function.PageAttribute?.AuthType ?? WebServerAuthType.None;
                if (authType != WebServerAuthType.None)
                {
                    content.DivOpen(Bootstrap4.Item.col, "col-12 col-sm-auto flex-sm-last");
                    AddBadges(content, function.PageAttribute);
                    content.DivClose(Bootstrap4.Item.col);
                }
                content.DivOpen(Bootstrap4.Item.col);
                content.AddHtml("<h4>");
                content.AddHtml(function.Method.Name.SplitCamelCase().Join(" "));
                if (function.Parameters.Length > 0)
                {
                    content.AddHtml(" (");
                    content.AddHtml(function.ParameterString());
                    content.AddHtml(")");
                }
                content.AddHtml("</h4>");
                content.DivClose(Bootstrap4.Item.col);
                content.DivClose(Bootstrap4.Item.row);

                foreach (string path in item)
                {
                    content.DivOpen(Bootstrap4.Item.row);
                    content.DivOpen(Bootstrap4.Item.col);
                    content.Link(path, $"Explain?function={path}");
                    content.DivClose(Bootstrap4.Item.col);

                    content.DivOpen(Bootstrap4.Item.col, "col-12 col-sm-auto");
                    content.Link("html", path + ".html", "btn btn-sm btn-outline-primary");
                    content.Link("json", path + ".json", "btn btn-sm btn-outline-primary");
                    content.Link("xml", path + ".xml", "btn btn-sm btn-outline-primary");
                    content.Link("plain", path + ".txt", "btn btn-sm btn-outline-primary");
                    content.DivClose(Bootstrap4.Item.col);
                    content.DivClose(Bootstrap4.Item.row);
                }

                if (doc?.Summary != null)
                {
                    content.DivOpen(Bootstrap4.Item.row);
                    content.DivOpen(Bootstrap4.Item.col);
                    content.AddHtml("<strong>Description:</strong>");
                    int cdata = doc.Summary.IndexOf("<![CDATA[");
                    if (cdata > -1)
                    {
                        content.Text(doc.Summary.Substring(0, cdata));
                        content.AddHtml("<br/><code>");
                        string code = doc.Summary.Substring(cdata + 9);
                        int cdataEnd = code.IndexOf("]]>");
                        content.AddHtml(code.Substring(0, cdata));
                        content.AddHtml("</code>");
                        content.Text(doc.Summary.Substring(cdata + cdataEnd + 9 + 3));
                    }
                    else
                    {
                        content.Text(doc.Summary);
                    }
                    content.DivClose(Bootstrap4.Item.col);
                    content.DivClose(Bootstrap4.Item.row);
                }
                content.AddHtml("</div>");
                content.ListGroupItemClose();
            }
            content.ListGroupClose();
            content.AddHtml("&nbsp;");
            WebMessage message = WebMessage.Create("Explain", functions == null ? "Explain functions." : $"Explain {functions} functions.");
            data.Answer = html.ToAnswer(message);
        }

        void FieldHtml(Bootstrap4 content, int i, object value, XNetDocItem doc)
        {
            content.ListGroupItemOpen("justify-content-between" + (0 == i % 2 ? " list-group-item-info" : null));
            if (value is FieldProperties field)
            {
                content.ParagraphText($"{field.DataType} {field.Name}", "col-2");
                content.Paragraph(doc?.Summary, "col-8");
                content.ParagraphOpen("col-2");
                if (field.StringEncoding != StringEncoding.Undefined)
                {
                    content.Badge(field.StringEncoding.ToString(), "badge-primary badge-pill float-right");
                }

                if (field.DataType == DataType.DateTime)
                {
                    content.Badge(field.DateTimeKind.ToString(), "badge-danger badge-pill float-right");
                    content.Badge(field.DateTimeType.ToString(), "badge-danger badge-pill float-right");
                }
                content.ParagraphClose();
            }
            else
            {
                content.ParagraphText(value.ToString(), "col-2");
                content.Paragraph(doc?.Summary, "col-8");
                content.ParagraphOpen();
                content.Badge(Convert.ToInt64(value).ToString(), "badge-default badge-pill");
                content.ParagraphClose();
            }
            content.ListGroupItemClose();
        }

        void ParameterHtml(Bootstrap4 content, int i, ParameterInfo parameter, XNetDocItem xMethodDoc)
        {
            content.ListGroupItemOpen("justify-content-between" + (0 == i % 2 ? " list-group-item-info" : null));

            string typeName = parameter.ParameterType.Name;
            if (parameter.ParameterType.Name == "Nullable`1")
            {
                typeName = parameter.ParameterType.GetGenericArguments().Last().Name;
                //typeName = parameter.ParameterType.GenericTypeArguments.Last().Name;
            }

            content.ParagraphText($"{typeName} {parameter.Name}", "col-sm-2 col-12");
            {
                string doc = null;
                xMethodDoc?.Parameters.TryGetValue(parameter.Name, out doc);
                Paragraph("col-sm-8 col-12", content, doc, null);
                //content.ParagraphText(doc, "col-8");
            }
            content.ParagraphOpen("col-sm-2 col-12");
            if (parameter.IsOptional)
            {
                content.Badge("optional", "badge-primary badge-pill float-right");
            }

            content.ParagraphClose();

            content.ListGroupItemClose();
        }
    }
}