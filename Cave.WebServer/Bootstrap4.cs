using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Cave.Web
{
    /// <summary>
    /// Bootstrap 4 content builder.
    /// </summary>
    public class Bootstrap4
    {
        /// <summary>Small and adaptive tag for adding context to just about any content.</summary>
        /// <param name="text">The text.</param>
        /// <param name="subclasses">The subclasses. (badge-[color], badge-pill).</param>
        /// <returns></returns>
        public static string GetBadge(string text, string subclasses = null)
        {
            return $"<span class=\"badge {subclasses}\">{WebUtility.HtmlEncode(text)}</span>";
        }

        /// <summary>Gets a link.</summary>
        /// <param name="text">The text.</param>
        /// <param name="link">The link.</param>
        /// <param name="subclasses">The subclasses. (button).</param>
        /// <returns></returns>
        public static string GetLink(string text, string link = null, string subclasses = null)
        {
            if (link == null)
            {
                link = text.StartsWith("/") ? text : "/" + text;
            }
            if (subclasses == null)
            {
                return $"<a href=\"{link}\">{WebUtility.HtmlEncode(text)}</a>";
            }

            return $"<a class=\"{subclasses}\" href=\"{link}\">{WebUtility.HtmlEncode(text)}</a>";
        }

        /// <summary>
        /// item types for divs / sections / ident.
        /// </summary>
        public enum Item
        {
#pragma warning disable 1591, SA1300, SA1602
            card,
            card_header,
            card_block,
            list_group,
            list_group_item,
            container,
            container_fluid,
            row,
            col,
            table,
            table_row,
            d_flex,
            p,
            float_left,
            float_right,
            btn_group,
            btn,
#pragma warning restore 1591, SA1300, SA1602
        }

        readonly Stack<Item> ident = new Stack<Item>();
        readonly StringBuilder content = new StringBuilder();

        void Check(Item item)
        {
            if (ident.Peek() != item)
            {
                throw new InvalidOperationException($"{item} expected!");
            }
        }

        void Ident(Item type)
        {
            ident.Push(type);
        }

        void UnIdent(Item type)
        {
            Item check = ident.Pop();
            if (check != type)
            {
                throw new Exception("Invalid unident!");
            }
        }

        string AutoIdent => new string('\t', ident.Count);

        /// <summary>Adds the specified HTML.</summary>
        /// <param name="html">The HTML.</param>
        public void AddHtml(string html)
        {
            content.Append(AutoIdent);
            content.AppendLine(html);
        }

        #region div

        /// <summary>Open a div.</summary>
        /// <param name="type">The type.</param>
        /// <param name="subclasses">The subclasses.</param>
        public void DivOpen(Item type, string subclasses = null)
        {
            content.Append(AutoIdent);
            content.Append("<div class=\"");
            content.Append(type.ToString().Replace('_', '-'));
            if (subclasses != null)
            {
                content.Append(" ");
                content.Append(subclasses);
            }
            content.AppendLine("\">");
            Ident(type);
        }

        /// <summary>Closes a div.</summary>
        /// <param name="type">The type.</param>
        public void DivClose(Item type)
        {
            UnIdent(type);
            content.Append(AutoIdent);
            content.AppendLine("</div>");
        }

        /// <summary>Creates a div.</summary>
        /// <param name="type">The type.</param>
        /// <param name="html">The HTML.</param>
        /// <param name="subclasses">The subclasses.</param>
        public void Div(Item type, string html, string subclasses = null)
        {
            DivOpen(type, subclasses);
            AddHtml(html);
            DivClose(type);
        }
        #endregion

        #region list

        /// <summary>opens a list group.</summary>
        /// <param name="subclasses">The subclasses.</param>
        public void ListGroupOpen(string subclasses = null)
        {
            if (subclasses == null)
            {
                AddHtml("<ul class=\"list-group\">");
            }
            else
            {
                AddHtml($"<ul class=\"list-group {subclasses}\">");
            }

            Ident(Item.list_group);
        }

        /// <summary>closes a list group.</summary>
        public void ListGroupClose()
        {
            UnIdent(Item.list_group);
            AddHtml("</ul>");
        }

        /// <summary>opens a list group item.</summary>
        /// <param name="subclasses">The subclasses.</param>
        public void ListGroupItemOpen(string subclasses = null)
        {
            if (subclasses == null)
            {
                AddHtml("<li class=\"list-group-item\">");
            }
            else
            {
                AddHtml($"<li class=\"list-group-item {subclasses}\">");
            }

            Ident(Item.list_group_item);
        }

        /// <summary>closes a list group item.</summary>
        public void ListGroupItemClose()
        {
            UnIdent(Item.list_group_item);
            AddHtml("</li>");
        }
        #endregion

        #region card

        /// <summary>opens a card with the specified header text.</summary>
        /// <param name="header">The header.</param>
        /// <param name="subclasses">The subclasses.</param>
        /// <param name="headerSubclasses">The header subclasses.</param>
        public void CardOpenText(string header = null, string subclasses = null, string headerSubclasses = null)
        {
            DivOpen(Item.card, subclasses);
            if (header != null)
            {
                DivOpen(Item.card_header, headerSubclasses);
                AddHtml(WebUtility.HtmlEncode(header));
                DivClose(Item.card_header);
            }
            DivOpen(Item.card_block);
        }

        /// <summary>opens a card with the specified html header.</summary>
        /// <param name="header">The header.</param>
        /// <param name="subclasses">The subclasses.</param>
        /// <param name="headerSubclasses">The header subclasses.</param>
        public void CardOpen(string header = null, string subclasses = null, string headerSubclasses = null)
        {
            DivOpen(Item.card, subclasses);
            if (header != null)
            {
                DivOpen(Item.card_header, headerSubclasses);
                AddHtml(header);
                DivClose(Item.card_header);
            }
            DivOpen(Item.card_block);
        }

        /// <summary>closes a card.</summary>
        public void CardClose()
        {
            DivClose(Item.card_block);
            DivClose(Item.card);
        }

        /// <summary>Creates a card with the specified content.</summary>
        /// <param name="header">The header.</param>
        /// <param name="paragraphs">The paragraphs.</param>
        public void Card(string header, params string[] paragraphs)
        {
            CardOpenText(header);
            foreach (string para in paragraphs)
            {
                Paragraph(para);
            }
            CardClose();
        }

        #endregion

        #region table

        /// <summary>Open a new table.</summary>
        /// <param name="columns">The columns.</param>
        /// <param name="subclasses">The subclasses.</param>
        public void TableOpen(IEnumerable<string> columns, string subclasses = null)
        {
            AddHtml($"<table class=\"table {subclasses}\">");
            Ident(Item.table);
            content.Append(AutoIdent);
            content.Append("<thead><tr>");
            foreach (string col in columns)
            {
                content.Append("<th class=\"header\">");
                content.Append(WebUtility.HtmlEncode(col));
                content.Append("</th>");
            }
            content.AppendLine("</tr></thead>");
            AddHtml("<tbody>");
            Ident(Item.table);
        }

        /// <summary>Opens a new row at the table.</summary>
        /// <param name="subclasses">The subclasses.</param>
        public void TableRowOpen(string subclasses = null)
        {
            Check(Item.table);
            AddHtml($"<tr class=\"{subclasses}\">");
            Ident(Item.table_row);
        }

        /// <summary>Creates a cell at the table.</summary>
        /// <param name="text">The text.</param>
        /// <param name="subclasses">The subclasses.</param>
        public void TableCell(string text, string subclasses = null)
        {
            TableHtmlCell(WebUtility.HtmlEncode(text));
        }

        /// <summary>Creates a cell at the table.</summary>
        /// <param name="html">The HTML.</param>
        /// <param name="subclasses">The subclasses.</param>
        public void TableHtmlCell(string html, string subclasses = null)
        {
            Check(Item.table_row);
            if (subclasses == null)
            {
                AddHtml($"<td>{html}</td>");
            }
            else
            {
                AddHtml($"<td class=\"{subclasses}\">{html}</td>");
            }
        }

        /// <summary>Closes the row.</summary>
        public void TableRowClose()
        {
            UnIdent(Item.table_row);
            AddHtml($"</tr>");
        }

        /// <summary>Closes the table.</summary>
        public void TableClose()
        {
            UnIdent(Item.table);
            AddHtml("</tbody>");
            UnIdent(Item.table);
            AddHtml("</table>");
        }
        #endregion

        #region paragraph

        /// <summary>Creates a new test paragraph.</summary>
        /// <param name="text">The text.</param>
        /// <param name="subclasses">The subclasses.</param>
        public void ParagraphText(string text, string subclasses = null)
        {
            Paragraph(WebUtility.HtmlEncode(text), subclasses);
        }

        /// <summary>Creates a new paragraph.</summary>
        /// <param name="html">The HTML.</param>
        /// <param name="subclasses">The subclasses.</param>
        public void Paragraph(string html, string subclasses = null)
        {
            content.Append(AutoIdent);
            ParagraphOpen(subclasses);
            content.Append(html);
            ParagraphClose();
        }

        /// <summary>Closes a paragraph.</summary>
        public void ParagraphClose()
        {
            UnIdent(Item.p);
            content.AppendLine("</p>");
        }

        /// <summary>Opens a new paragraph.</summary>
        /// <param name="subclasses">The subclasses.</param>
        public void ParagraphOpen(string subclasses = null)
        {
            if (subclasses != null)
            {
                content.Append($"<p class=\"{subclasses}\">");
            }
            else
            {
                content.Append("<p>");
            }
            Ident(Item.p);
        }
        #endregion

        /// <summary>Images the specified link.</summary>
        /// <param name="link">The link.</param>
        /// <param name="subclasses">The subclasses.</param>
        public void Image(string link, string subclasses = null)
        {
            content.Append($"<img src=\"{link}\"");
            if (subclasses != null)
            {
                content.Append($" class=\"{subclasses}\"");
            }

            content.Append("/>");
        }

        /// <summary>Creates a badge.</summary>
        /// <param name="text">The text.</param>
        /// <param name="subclasses">The subclasses.</param>
        public void Badge(string text, string subclasses = null)
        {
            AddHtml(GetBadge(text, subclasses));
        }

        /// <summary>Creates a link.</summary>
        /// <param name="text">The text.</param>
        /// <param name="link">The link.</param>
        /// <param name="subclasses">The subclasses.</param>
        public void Link(string text, string link = null, string subclasses = null)
        {
            AddHtml(GetLink(text, link, subclasses));
        }

        /// <summary>Creates a text.</summary>
        /// <param name="text">The text.</param>
        public void Text(string text)
        {
            AddHtml(WebUtility.HtmlEncode(text));
        }

        /// <summary>Returns a <see cref="string" /> that represents this instance.</summary>
        /// <returns>A <see cref="string" /> that represents this instance.</returns>
        public override string ToString()
        {
            return content.ToString();
        }

        /// <summary>Returns a hash code for this instance.</summary>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table.
        /// </returns>
        public override int GetHashCode()
        {
            return content.GetHashCode();
        }
    }
}
