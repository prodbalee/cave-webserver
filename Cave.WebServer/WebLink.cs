using System.Net;

namespace Cave.Web
{
    /// <summary>
    /// Provides a link and text.
    /// </summary>
    public struct WebLink
    {
        /// <summary>The link.</summary>
        public string Link;

        /// <summary>The text.</summary>
        public string Text;

        /// <summary>Returns a <see cref="string" /> that represents this instance.</summary>
        /// <returns>A <see cref="string" /> that represents this instance.</returns>
        public override string ToString()
        {
            string text = Text ?? "root";
            if (string.IsNullOrEmpty(Link))
            {
                return WebUtility.HtmlEncode(text);
            }
            return $"<a href=\"{Link}\">{WebUtility.HtmlEncode(text)}</a>";
        }
    }
}
