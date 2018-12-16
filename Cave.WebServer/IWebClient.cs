using System.Net;

namespace Cave.Web
{
    /// <summary>
    /// Provides a web client interface
    /// </summary>
    public interface IWebClient
    {
        /// <summary>Gets the local end point.</summary>
        /// <value>The local end point.</value>
        IPEndPoint LocalEndPoint { get; }

        /// <summary>Gets the remote end point.</summary>
        /// <value>The remote end point.</value>
        IPEndPoint RemoteEndPoint { get; }
    }
}
