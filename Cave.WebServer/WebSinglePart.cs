using System.Collections.Generic;
using System.Diagnostics;
using Cave.Collections.Generic;

namespace Cave.Web
{
    /// <summary>
    /// Provides a part of a multi part content
    /// </summary>
    [DebuggerDisplay("{ToString()}")]
    public class WebSinglePart
    {
        /// <summary>Gets the headers.</summary>
        /// <value>The headers.</value>
        public Dictionary<string, string> Headers { get; } = new Dictionary<string, string>();

        /// <summary>Gets or sets the content.</summary>
        /// <value>The content.</value>
        public byte[] Content { get; set; }

        /// <summary>Gets the name of the file read from the <see cref="ContentDisposition"/>.</summary>
        /// <value>The name of the file.</value>
        public string FileName
        {
            get
            {
                if (ContentDisposition != null)
                {
                    OptionCollection options = OptionCollection.FromStrings(ContentDisposition.Split(';'), true);
                    if (options.Contains("filename"))
                    {
                        return options["filename"].Value;
                    }
                }
                return null;
            }
        }

        /// <summary>Gets the name of the part read from the <see cref="ContentDisposition"/>.</summary>
        /// <value>The name.</value>
        public string Name
        {
            get
            {
                if (ContentDisposition != null)
                {
                    OptionCollection options = OptionCollection.FromStrings(ContentDisposition.Split(';'), true);
                    if (options.Contains("name"))
                    {
                        return options["name"].Value;
                    }
                }
                return null;
            }
        }

        /// <summary>Gets the content disposition.</summary>
        /// <value>The content disposition.</value>
        public string ContentDisposition
        {
            get
            {
                Headers.TryGetValue("content-disposition", out string value);
                return value;
            }
        }

        /// <summary>Gets the type of the content.</summary>
        /// <value>The type of the content.</value>
        public string ContentType
        {
            get
            {
                Headers.TryGetValue("content-type", out string value);
                return value;
            }
        }

        /// <summary>Gets the content transfer encoding.</summary>
        /// <value>The content transfer encoding.</value>
        public string ContentTransferEncoding
        {
            get
            {
                Headers.TryGetValue("content-transfer-encoding", out string value);
                return value;
            }
        }

        /// <summary>Returns a <see cref="System.String" /> that represents this instance.</summary>
        /// <returns>A <see cref="System.String" /> that represents this instance.</returns>
        public override string ToString()
        {
            if (Content == null)
            {
                return Name;
            }
            if (FileName == null)
            {
                return $"{Name} ({Content.Length})";
            }
            return $"{Name} '{FileName}' ({Content.Length})";
        }
    }
}
