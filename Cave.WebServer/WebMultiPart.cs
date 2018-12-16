using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Cave.IO;

namespace Cave.Web
{
    /// <summary>
    /// Provides multi part content
    /// </summary>
    public class WebMultiPart
    {
        /// <summary>Gets the parts.</summary>
        /// <value>The parts.</value>
        public List<WebSinglePart> Parts { get; } = new List<WebSinglePart>();

        /// <summary>Parses from the specified reader.</summary>
        /// <param name="reader">The reader.</param>
        /// <param name="boundary">The boundary.</param>
        /// <returns></returns>
        /// <exception cref="InvalidDataException">
        /// Invalid header in multi part content!
        /// or
        /// Invalid data after multi part content!
        /// </exception>
        public static WebMultiPart Parse(DataReader reader, string boundary)
        {
            boundary = "--" + boundary;
            byte[] binaryBoundary = Encoding.UTF8.GetBytes(boundary);
            WebMultiPart result = new WebMultiPart();
            //first part is main content, we do not have addition headers there
            bool inHeader = true;
            WebSinglePart part = new WebSinglePart();
            while (true)
            {
                if (inHeader)
                {
                    string line = reader.ReadLine();
                    //end of header ?
                    if (line.Length == 0) { inHeader = false; continue; }
                    //boundary ?
                    if (line.StartsWith("--"))
                    {
                        //end of data ?
                        //if (line == "--") break;
                        //next part ?
                        if (line == boundary) { part = new WebSinglePart(); continue; }
                        throw new WebException(WebError.UnknownContent, 0, "Invalid header in multi part content!");
                    }
                    //read header value
                    string[] kv = line.Split(new char[] { ':' }, 2);
                    if (kv.Length < 2)
                    {
                        throw new WebException(WebError.UnknownContent, 0, "Invalid header in multi part content!");
                    }

                    part.Headers.Add(kv[0].Trim().ToLower(), kv[1].Trim());
                }
                else //content
                {
                    byte[] buffer = new byte[1024 * 1024];
                    int offset = 0;
                    reader.ReadUntil(buffer, ref offset, false, binaryBoundary);
                    Array.Resize(ref buffer, offset);
                    part.Content = buffer;
                    result.Parts.Add(part);
                    part = new WebSinglePart();
                    inHeader = true;
                    string endOfData = reader.ReadLine();
                    //end of data ?
                    if (endOfData == "--")
                    {
                        //var empty = reader.ReadLine();
                        //if (empty != "") throw new CaveWebException(CaveWebError.UnknownContent, "Invalid data after end of multi part content!");
                        break;
                    }
                    if (endOfData.Length != 0)
                    {
                        throw new WebException(WebError.UnknownContent, 0, "Invalid data after multi part content!");
                    }
                }
            }
            return result;
        }

        /// <summary>Tries to get the part with the specified name.</summary>
        /// <param name="name">The name.</param>
        /// <param name="part">The part.</param>
        /// <returns>Returns true if the part was retrieved.</returns>
        public bool TryGet(string name, out WebSinglePart part)
        {
            foreach (WebSinglePart p in Parts)
            {
                if (p.Name == name) { part = p; return true; }
            }
            part = null;
            return false;
        }
    }
}
