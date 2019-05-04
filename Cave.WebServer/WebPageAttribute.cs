using System;
using System.Collections.Generic;
using System.Linq;

namespace Cave.Web
{
    /// <summary>
    /// Provides an attribute for defining entry points of the <see cref="WebServer"/> class.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class WebPageAttribute : Attribute
    {
        /// <summary>Gets or sets the authentication data.</summary>
        /// <value>The authentication data.</value>
        public string AuthData { get; set; }

        /// <summary>Gets or sets the web paths the function is inserted at.</summary>
        /// <value>The comma separated web paths.</value>
        public string Paths { get; set; }

        /// <summary>Gets or sets the type of the authentication.</summary>
        /// <value>The type of the authentication.</value>
        public WebServerAuthType AuthType { get; set; }

        /// <summary>Gets or sets the allow headers.</summary>
        /// <value>The allow headers.</value>
        public string AllowHeaders { get; set; }

        /// <summary>Gets or sets a value indicating whether [allow any parameters].</summary>
        /// <value><c>true</c> if [allow any parameters]; otherwise, <c>false</c>.</value>
        public bool AllowAnyParameters { get; set; }

        /// <summary>Gets the paths.</summary>
        /// <returns></returns>
        public IEnumerable<string> GetPaths()
        {
            if (Paths == null)
            {
                return new string[0];
            }

            return Paths.Split(new char[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim());
        }

        /// <summary>Initializes a new instance of the <see cref="WebPageAttribute"/> class.</summary>
        public WebPageAttribute()
        {
        }
    }
}
