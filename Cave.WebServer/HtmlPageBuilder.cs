using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Cave.Data;
using Cave.Net;

namespace Cave.Web
{
    /// <summary>
    /// Provides available html parts or the result.
    /// </summary>
    public sealed class HtmlPageBuilder
    {
        readonly WebRequest request;

        /// <summary>
        /// Initializes a new instance of the <see cref="HtmlPageBuilder"/> class.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">request.</exception>
        public HtmlPageBuilder(WebRequest request)
        {
            this.request = request ?? throw new ArgumentNullException("request");
            AddHeader(Properties.Resources.CaveWebServerHeaders);
            AddFooter($"&nbsp;<hr><address>{request.Server.ServerVersionString} Server at {NetTools.HostName} Port {request.LocalPort}<br>&copy {request.Server.ServerCopyRight}</address>");
            string protocol = (request.Server.Certificate == null) ? "http://" : "https://";
            string link = protocol + request.Headers["host"];
            Breadcrump.Add(new WebLink() { Link = link, Text = request.Server.Title });
            foreach (string part in request.DecodedUrl.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
            {
                link = link.TrimEnd('/') + "/" + part;
                Breadcrump.Add(new WebLink() { Text = part, Link = link });
            }
        }

        /// <summary>Gets the footer.</summary>
        public StringBuilder Footer { get; } = new StringBuilder();

        /// <summary>Gets the header.</summary>
        public StringBuilder Header { get; } = new StringBuilder();

        /// <summary>Gets the content.</summary>
        public Bootstrap4 Content { get; } = new Bootstrap4();

        /// <summary>Gets the breadcrump.</summary>
        public List<WebLink> Breadcrump { get; } = new List<WebLink>();

        /// <summary>Initializes a new instance of the <see cref="HtmlPageBuilder"/> class.</summary>
        public HtmlPageBuilder() { }

        /// <summary>Closes the table.</summary>
        public void CloseTable()
        {
            Content.TableClose();
            Content.CardClose();
            Content.AddHtml("&nbsp;");
        }

        /// <summary>Writes a row at the last opened table.</summary>
        /// <param name="r">The row.</param>
        public void WriteRow(Row r)
        {
            Content.TableRowOpen();
            foreach (object o in r.GetValues())
            {
                Content.TableCell(StringExtensions.ToString(o));
            }
            Content.TableRowClose();
        }

        /// <summary>Starts a new table.</summary>
        /// <param name="tableName">Name of the table.</param>
        /// <param name="layout">The layout.</param>
        public void StartTable(string tableName, RowLayout layout)
        {
            Content.CardOpenText($"Table: {tableName}");
            Content.ParagraphText($"{layout.FieldCount} fields");
            Content.TableOpen(layout.Fields.Select(f => f.Name), "table-striped table-responsive");
        }

        /// <summary>Adds text to the header (HTML only).</summary>
        /// <param name="content">The content.</param>
        public void AddHeader(object content)
        {
            Header.AppendLine(content.ToString());
        }

        /// <summary>Adds text to the footer (HTML only).</summary>
        /// <param name="content">The content.</param>
        public void AddFooter(object content)
        {
            Footer.AppendLine(content.ToString());
        }

        /// <summary>Returns the result as HTML answer.</summary>
        /// <returns></returns>
        public WebAnswer ToAnswer(WebMessage message)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine($"<html><head><title>{WebUtility.HtmlEncode(message.Source)}</title>");
            sb.Append(Header);
            sb.AppendLine("</head><body>");
            sb.AppendLine("<div class=\"container-fluid\">");
            if (Breadcrump != null)
            {
                sb.AppendLine("<ol class=\"breadcrumb\">");
                WebLink[] links = Breadcrump.ToArray();
                int last = links.Length - 1;
                for (int i = 0; i < links.Length; i++)
                {
                    if (i != last)
                    {
                        sb.Append("<li class=\"breadcrumb-item\">");
                    }
                    else
                    {
                        sb.Append("<li class=\"breadcrumb-item active\">");
                    }

                    sb.Append(links[i]);
                    sb.Append("</li>");
                }
                sb.AppendLine("</ol>");
            }
            if (message.Error == WebError.None)
            {
                sb.AppendLine("<div class=\"alert alert-success\" role=\"alert\">");
            }
            else
            {
                sb.AppendLine("<div class=\"alert alert-danger\" role=\"alert\">");
            }
            sb.Append($"<strong>{(int)message.Code} {message.Code} {WebUtility.HtmlEncode(message.Source)}");
            if (message.Error != WebError.None)
            {
                sb.Append($" ({message.Error})");
            }

            sb.Append("</strong><p>");
            sb.AppendLine(WebUtility.HtmlEncode(message.Content));
            sb.AppendLine("</p></div>");
            sb.Append(Content);
            sb.Append(Footer);
            sb.AppendLine("</div>");
            sb.AppendLine("</body></html>");
            return WebAnswer.Html(request, message, sb.ToString());
        }
    }
}
