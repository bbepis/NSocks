using System;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using NSocks.Socks;

namespace NSocks
{
	/// <summary>
	/// A HttpClientHandler that sends requests to a Socks5 proxy, instead of directly via the HTTP stack.
	/// </summary>
	public class Socks5Handler : HttpClientHandler
	{
		private Uri ProxyUri { get; set; }

		/// <summary>
		/// If true, resolves hostnames locally, otherwise false to get the Socks5 server to resolve them.
		/// </summary>
		public bool ResolveDnsLocally { get; set; } = true;

		/// <summary>
		/// Creates a <see cref="Socks5Handler"/> instance using a Uri of the proxy server.
		/// </summary>
		/// <param name="proxyUri">The URI to use. Only the host and port is used, however if user login data is specified, it is also used.</param>
		public Socks5Handler(Uri proxyUri)
		{
			ProxyUri = proxyUri;

			if (!string.IsNullOrEmpty(proxyUri.UserInfo) && proxyUri.UserInfo.Contains(':'))
			{
				var splitUserInfo = ProxyUri.UserInfo.Split(':');
				Credentials = new NetworkCredential(
							HttpUtility.UrlDecode(splitUserInfo[0]),
							HttpUtility.UrlDecode(splitUserInfo[1]));
			}
			else
			{
				Credentials = null;
			}
		}

		/// <summary>
		/// Creates a <see cref="Socks5Handler"/> instance using a Uri of the proxy server, and explicit user credentials.
		/// </summary>
		/// <param name="proxyUri">The URI to use. Only the host and port is used.</param>
		/// <param name="username">The username of the credentials.</param>
		/// <param name="password">The password of the credentials.</param>
		public Socks5Handler(Uri proxyUri, string username, string password)
		{
			ProxyUri = proxyUri;
			Credentials = new NetworkCredential(username, password);
		}

		/// <inheritdoc />
		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();

			// Create the underlying socket to the proxy server, and connect to it

			using var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);

			var resolvedAddresses = await Dns.GetHostAddressesAsync(ProxyUri.Host);

			if (resolvedAddresses.Length == 0)
				throw new InvalidOperationException("Unable to resolve proxy hostname");

			

			await socket.ConnectAsync(resolvedAddresses[0], ProxyUri.Port);

			await using var networkStream = new NetworkStream(socket, true);



			// Connect to the socks tunnel and set up the connection

			Socks5Tunnel.PerformHandshake(networkStream, (NetworkCredential)Credentials);
			Socks5Tunnel.SendConnectRequest(networkStream, request.RequestUri, ResolveDnsLocally);

			// Build the SSL wrapper if this is a HTTPS connection
			// Unfortunately you can't set 'using' variables, so we have to wrap this all in a try/finally statement

			SslStream sslStream = null;

			try
			{
				if (request.RequestUri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
				{
					sslStream = new SslStream(networkStream, true);
					sslStream.AuthenticateAsClient(request.RequestUri.Host, null,
						SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12, true);
				}

				// Build and send request

				await RequestSerializer.WriteRequestToStreamAsync(networkStream, request, CookieContainer);

				// Receive and parse response

				var builder = new ResponseSerializer(networkStream);
				return await builder.DeserializeResponseAsync(CookieContainer);
			}
			finally
			{
				sslStream?.Dispose();
			}
		}
	}
}