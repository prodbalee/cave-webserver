#if NET20 || NET35
using System.Web;

namespace System.Net
{
	/// <summary>
	/// Provides a backport of the WebUtility class
	/// </summary>
	public class WebUtility
	{
		/// <summary>
		/// Converts a string to an HTML-encoded string.
		/// </summary>
		/// <param name="text"></param>
		/// <returns></returns>
		public static string HtmlEncode(string text)
		{
			return HttpUtility.HtmlEncode(text);
		}

		/// <summary>
		/// Converts a string that has been HTML-encoded for HTTP transmission into a decoded string.
		/// </summary>
		/// <param name="text"></param>
		/// <returns></returns>
		public static string HtmlDecode(string text)
		{
			return HttpUtility.HtmlDecode(text);
		}
	}
}
#elif NETSTANDARD20 || NET40 || NET45 || NET46 || NET47
#else
#error No code defined for the current framework or NETXX version define missing!
#endif