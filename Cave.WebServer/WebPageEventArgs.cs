using System;

namespace Cave.Web
{
    /// <summary>
    /// Cave web page call
    /// </summary>
    /// <seealso cref="System.EventArgs" />
    public class WebPageEventArgs : EventArgs
    {
        bool handled;

        /// <summary>Gets the data.</summary>
        /// <value>The data.</value>
        public WebData Data { get; }

        /// <summary>Gets or sets a value indicating whether this <see cref="WebPageEventArgs"/> is handled.</summary>
        /// <value><c>true</c> if handled; otherwise, <c>false</c>.</value>
        public bool Handled { get => handled; set => handled |= value; }

        /// <summary>Initializes a new instance of the <see cref="WebPageEventArgs"/> class.</summary>
        /// <param name="data">The data.</param>
        public WebPageEventArgs(WebData data)
        {
            Data = data;
        }
    }
}