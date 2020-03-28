using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NSocks.Response;

namespace NSocks
{
	/// <summary>
	/// Handles converting a network stream response into a <see cref="HttpResponseMessage"/> object.
	/// </summary>
	public class ResponseSerializer
	{
		private Stream Stream { get; set; }

		private const int BufferSize = 4096;

		private readonly byte[] readBuffer = new byte[BufferSize];
		private int readBufferIndex = 0;
		private int readBufferLength = 0;

		public ResponseSerializer(Stream stream)
		{
			Stream = stream;
		}

		private static Regex HttpVersionRegex = new Regex(@"^HTTP\/(\d\.\d).+?(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
		private static Regex HttpHeaderRegex = new Regex(@"^(.+?):\s*?(.+)$", RegexOptions.Compiled);

		/// <summary>
		/// Deserializes a HTTP network response into a <see cref="HttpResponseMessage"/> object.
		/// </summary>
		/// <param name="cookieContainer">The cookie container to use for populating with Set-Cookie values. Optional.</param>
		public async Task<HttpResponseMessage> DeserializeResponseAsync(CookieContainer cookieContainer = null)
		{
			var response = new HttpResponseMessage();
			var contentHeaders = new List<(string, string[])>();

			// Parse the response line

			var statusLine = await ReadLineAsync();
			var statusMatch = HttpVersionRegex.Match(statusLine);

			if (!statusMatch.Success)
			{
				throw new Exception("Received empty/malformed response");
			}

			response.Version = Version.Parse(statusMatch.Groups[1].Value);
			response.StatusCode = (HttpStatusCode)int.Parse(statusMatch.Groups[2].Value);

			// Read headers

			while (true)
			{
				string line = await ReadLineAsync();

				if (line == string.Empty)
				{
					// End of response
					return response;
				}

				if (line == "\r\n")
				{
					// End of header section
					break;
				}

				var match = HttpHeaderRegex.Match(line);

				if (!match.Success)
				{
					throw new Exception("Malformed response section");
				}

				string key = match.Groups[1].Value;

				var values = match.Groups[2].Value.Split(',')
								  .Select(value => value.Trim());

				if (!ResponseHelper.IsContentHeader(key))
				{
					response.Headers.TryAddWithoutValidation(key, values);
				}
				else
				{
					contentHeaders.Add((key, values.ToArray()));
				}
			}

			// Read content

			if (contentHeaders.Count > 0)
			{
				response.Content = await CreateResponseContentStream(response.Headers, contentHeaders);
			}

			return response;
		}

		private async Task<HttpContent> CreateResponseContentStream(HttpResponseHeaders headers, IList<(string, string[])> contentHeaders)
		{
			var remainingBufferStream = new MemoryStream(readBuffer, readBufferIndex, readBufferLength - readBufferIndex);
			Stream baseStream = new MultiPartStream(new[] { remainingBufferStream, Stream });

			if (headers.Contains("Transfer-Encoding"))
			{
				throw new NotImplementedException("Chunked responses are currently not supported");
			}

			string contentEncoding = ResponseHelper.GetContentEncoding(contentHeaders);

			if (contentEncoding != null)
			{
				switch (contentEncoding)
				{
					case "gzip":
						baseStream = new GZipStream(baseStream, CompressionMode.Decompress, false);
						break;
					case "deflate":
						baseStream = new DeflateStream(baseStream, CompressionMode.Decompress, false);
						break;
					default:
						throw new InvalidOperationException($"Unknown encoding format '{contentEncoding}'");
				}
			}

			var content = new StreamContent(baseStream);

			foreach (var contentHeader in contentHeaders)
			{
				content.Headers.TryAddWithoutValidation(contentHeader.Item1, contentHeader.Item2);
			}

			return content;
		}

		private async Task<string> ReadLineAsync()
		{
			int startIndex = readBufferIndex;

			StringBuilder builder = new StringBuilder();

			void dumpToBuilder()
			{
				if (readBufferIndex - startIndex > 0)
				{
					builder.Append(Encoding.ASCII.GetString(readBuffer.AsSpan(startIndex, readBufferIndex - startIndex)));
				}
			}

			while (true)
			{
				if (readBufferIndex >= readBufferLength)
				{
					dumpToBuilder();

					readBufferIndex = 0;
					startIndex = 0;

					readBufferLength = await Stream.ReadAsync(readBuffer, 0, BufferSize);

					if (readBufferLength <= 0)
					{
						return builder.ToString();
					}
				}

				if (readBuffer[readBufferIndex++] == (byte)'\n')
				{
					dumpToBuilder();

					return builder.ToString();
				}
			}
		}
	}
}