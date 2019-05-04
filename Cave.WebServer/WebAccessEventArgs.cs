using System;

namespace Cave.Web
{
    /// <summary>
    /// Cave web page call.
    /// </summary>
    /// <seealso cref="EventArgs" />
    public class WebAccessEventArgs : EventArgs
    {
        bool denied;

        /// <summary>Gets the data.</summary>
        /// <value>The data.</value>
        public WebData Data { get; }

        /// <summary>Gets or sets a value indicating whether the call is denied.</summary>
        /// <value><c>true</c> if denied; otherwise, <c>false</c>.</value>
        /// <remarks>You can set this only once to true. All further set commands will be ignored.</remarks>
        public bool Denied { get => denied; set => denied |= value; }

        /// <summary>Initializes a new instance of the <see cref="WebAccessEventArgs"/> class.</summary>
        /// <param name="data">The data.</param>
        public WebAccessEventArgs(WebData data)
        {
            Data = data;
        }
    }
}
