using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace NSocks
{
	/// <summary>
	/// Contains methods relating to serializing a HTTP request to a network stream.
	/// </summary>
	public static class RequestSerializer
	{
		/// <summary>
		/// Serializes a <see cref="HttpRequestMessage"/> to it's equivalent wire format, and writes it directly into a stream.
		/// </summary>
		/// <param name="stream">The stream to write to.</param>
		/// <param name="request">The request to be sent.</param>
		/// <param name="cookieContainer">The cookie container to be used for generating the cookie header. Optional.</param>
		public static Task WriteRequestToStreamAsync(Stream stream, HttpRequestMessage request, CookieContainer cookieContainer = null)
		{
			if (request.Version.Major >= 2)
			{
				throw new ArgumentException("HTTP 2 and/or above are not supported with this serializer.", nameof(request));
			}

			if (request.Version.Major == 1 && request.Version.Minor == 1)
			{
				return WriteHttp11Message(stream, request, cookieContainer);
			}

			throw new ArgumentException($"Unrecognized HTTP version: HTTP/{request.Version}", nameof(request));
		}

		private static async Task WriteHttp11Message(Stream stream, HttpRequestMessage request, CookieContainer cookieContainer)
		{
			// HTTP 1.1 messages typically look like this:
			// <method + path + HTTP/1.1> \r\n
			// <request headers>
			// <content headers>
			// \r\n
			// data content

			// Headers are written as such:
			// <key>: <value1>(, <value2>, <valueX>) \r\n

			StringBuilder requestBuilder = new StringBuilder();
			requestBuilder.Append($"{request.Method.Method} {request.RequestUri.PathAndQuery} HTTP/1.1\r\n");

			// 'Host' is a required header. If it's not specified, use the request to determine it.

			if (string.IsNullOrWhiteSpace(request.Headers.Host))
				requestBuilder.Append($"Host: {request.RequestUri.Host}\r\n");

			// Write request headers

			foreach (var header in request.Headers)
			{
				if (!header.Value.Any())
					continue;

				string headerValues = string.Join(" ", header.Value);

				if (!string.IsNullOrWhiteSpace(headerValues))
					requestBuilder.Append($"{header.Key}: {headerValues}\r\n");
			}

			// If the cookie header isn't already specified, and we have a cookie set, generate the header ourselves

			if (!request.Headers.Contains("Cookie") && cookieContainer != null)
			{
				string cookiesValue = string.Join(", ",
					cookieContainer.GetCookies(request.RequestUri).Cast<Cookie>().Select(x => x.Value));

				if (!string.IsNullOrWhiteSpace(cookiesValue))
					requestBuilder.Append($"Cookie: {cookiesValue}\r\n");
			}

			if (request.Content != null)
			{
				foreach (var header in request.Content.Headers)
				{
					if (!header.Value.Any())
						continue;

					string headerValues = string.Join(" ", header.Value);
					requestBuilder.Append($"{header.Key}: {headerValues}\r\n");
				}

				if (request.Content.Headers.ContentLength.HasValue && request.Content.Headers.ContentLength.Value > 0)
				{
					requestBuilder.Append($"Content-Length: {request.Content.Headers.ContentLength}\r\n");
				}
			}

			requestBuilder.Append("\r\n");

			byte[] headerBytes = Encoding.ASCII.GetBytes(requestBuilder.ToString());

			await stream.WriteAsync(headerBytes, 0, headerBytes.Length);

			if (request.Content != null)
				await request.Content.CopyToAsync(stream);
		}
	}
}