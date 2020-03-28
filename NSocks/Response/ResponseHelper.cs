using System;
using System.Collections.Generic;
using System.Linq;

namespace NSocks.Response
{
	internal static class ResponseHelper
	{
		private static readonly string[] contentHeaders =
		{
			"Last-Modified",
			"Expires",
			"Content-Type",
			"Content-Range",
			"Content-MD5",
			"Content-Location",
			"Content-Length",
			"Content-Language",
			"Content-Encoding",
			"Allow"
		};

		/// <summary>
		/// Returns true if the header is related to the content, otherwise false. Case insensitive.
		/// </summary>
		/// <param name="name">The header to check.</param>
		internal static bool IsContentHeader(string name)
		{
			foreach (var header in contentHeaders)
			{
				if (header.Equals(name, StringComparison.OrdinalIgnoreCase))
					return true;
			}

			return false;
		}

		/// <summary>
		/// Get a lower case representation of the content encoding used by the response's content.
		/// </summary>
		/// <param name="contentHeaders">The headers of the content.</param>
		internal static string GetContentEncoding(IList<(string, string[])> contentHeaders)
		{
			foreach (var header in contentHeaders)
				if (header.Item1.Equals("Content-Encoding", StringComparison.OrdinalIgnoreCase))
					return header.Item2.FirstOrDefault();

			return null;
		}
	}
}