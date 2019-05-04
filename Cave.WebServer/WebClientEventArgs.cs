using System;

namespace Cave.Web
{
    /// <summary>
    /// Provides event arguments for <see cref="IWebClient"/>.
    /// </summary>
    /// <seealso cref="EventArgs" />
    public class WebClientEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="WebClientEventArgs"/> class.</summary>
        /// <param name="client">The client.</param>
        public WebClientEventArgs(IWebClient client)
        {
            Client = client;
        }

        /// <summary>Gets the client.</summary>
        /// <value>The client.</value>
        public IWebClient Client { get; }
    }
}
