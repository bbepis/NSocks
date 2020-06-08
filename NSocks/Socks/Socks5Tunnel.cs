using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NSocks.Socks
{
	// Code is based off of https://www.ietf.org/rfc/rfc1928.txt

	/// <summary>
	/// Contains method relating to communicating with a Socks5 server.
	/// </summary>
	public static class Socks5Tunnel
	{
		private const byte AuthNegotiationVersion = 0x01;
		private const byte Socks5Version = 0x05;

		/// <summary>
		/// Performs the initial handshake with a Socks5 server.
		/// </summary>
		/// <param name="networkStream">The network stream that's connected to the Socks5 server.</param>
		/// <param name="credentials">The credentials to use to log into the server with. Optional.</param>
		public static async Task PerformHandshake(Stream networkStream, CancellationToken token = default, NetworkCredential credentials = null)
		{
			await using var reader = new AsyncBinaryReader(networkStream, Encoding.ASCII, true);
			await using var writer = new AsyncBinaryWriter(networkStream, Encoding.ASCII, true);

			// Send handshake message

			var authenticationMethods = credentials != null
				? new[] { SocksAuthenticationType.NoAuthentication, SocksAuthenticationType.UsernamePassword }
				: new[] { SocksAuthenticationType.NoAuthentication };

			await WriteHandshakeMessage(writer, token, authenticationMethods);
			await writer.FlushAsync(token);

			// Receive handshake response

			byte[] handshakeBuffer = new byte[2];

			if (await reader.ReadAsync(handshakeBuffer, token) != 2)
				throw new InvalidDataException("Proxy response was invalid");

			if (handshakeBuffer[0] != Socks5Version)
				throw new InvalidDataException("Proxy response was invalid");

			switch ((SocksAuthenticationType)handshakeBuffer[1])
			{
				case SocksAuthenticationType.NoAuthentication:
					break;

				case SocksAuthenticationType.UsernamePassword:
				{
					if (credentials == null)
						throw new AuthenticationException("Authentication required but no credentials specified");

					await WriteAuthenticationMessage(writer, credentials.UserName, credentials.Password, token);
					await writer.FlushAsync(token);

					if (await reader.ReadAsync(handshakeBuffer, token) != 2)
						throw new InvalidDataException("Proxy response was invalid");

					if (handshakeBuffer[0] != AuthNegotiationVersion)
						throw new InvalidDataException("Proxy response was invalid");

					if (handshakeBuffer[1] != (byte)SocksAuthenticationType.NoAuthentication)
						throw new AuthenticationException("Proxy server rejected authentication credentials");

					break;
				}

				default:
					throw new InvalidDataException($"Unknown authentication type '{handshakeBuffer[1]}'");
			}
		}

		/// <summary>
		/// Sends a CONNECT request to the proxy. Requires the handshake to be initiated first.
		/// </summary>
		/// <param name="networkStream">The network stream that's connected to the Socks5 server.</param>
		/// <param name="destinationUri">The Uri of the host to connect to. Only the Host and Port properties are used.</param>
		/// <param name="resolveDnsLocally">True if to resolve the hostname to an IP address locally, otherwise false to get the Socks5 server to do it.</param>
		public static async Task SendConnectRequest(Stream networkStream, Uri destinationUri, bool resolveDnsLocally = true, CancellationToken token = default)
		{
			await using var reader = new AsyncBinaryReader(networkStream, Encoding.ASCII, true);
			await using var writer = new AsyncBinaryWriter(networkStream, Encoding.ASCII, true);

			await writer.WriteAsync(Socks5Version, token);
			await writer.WriteAsync((byte)SocksCommand.Connect, token);
			await writer.WriteAsync((byte)0, token); // Reserved

			if (!IPAddress.TryParse(destinationUri.Host, out var ipAddress))
			{
				if (resolveDnsLocally)
				{
					ipAddress = (await Dns.GetHostAddressesAsync(destinationUri.Host)).FirstOrDefault();

					if (ipAddress == null)
						throw new ArgumentException("Unable to resolve host", nameof(destinationUri));
				}
			}

			if (ipAddress != null)
			{
				var addressType = ipAddress.AddressFamily == AddressFamily.InterNetworkV6
					? SocksAddressType.IPv6
					: SocksAddressType.IPv4;

				await writer.WriteAsync((byte)addressType, token);
				await writer.WriteAsync(ipAddress.GetAddressBytes(), token);
			}
			else
			{
				await writer.WriteAsync((byte)SocksAddressType.DomainName, token);

				byte[] addressNameBytes = Encoding.ASCII.GetBytes(destinationUri.Host);

				await writer.WriteAsync((byte)addressNameBytes.Length, token);
				await writer.WriteAsync(addressNameBytes, token);
			}

			// We can't use the regular BinaryWriter.Write(uint) method here as it writes in little endian, when we need the port number written as big endian.
			// Plus this makes it more consistent across runtime implementations.

			await writer.WriteAsync((byte)(destinationUri.Port >> 8), token);
			await writer.WriteAsync((byte)(destinationUri.Port & 0xFF), token);

			await writer.FlushAsync(token);

			if (await reader.ReadByteAsync(token) != Socks5Version)
				throw new InvalidDataException("Proxy response was invalid");

			var replyType = (SocksRequestReplyType)await reader.ReadByteAsync(token);

			switch (replyType)
			{
				case SocksRequestReplyType.Succeeded:
					break;

				default:
					throw new Exception($"Socks server responded with error code: {replyType}");
			}

			// Since we are using the CONNECT command here, we do not need to parse the last part of the packet as they're only used for the BIND command
			// Just read them to empty the network buffer

			await reader.ReadByteAsync(token); // Reserved byte
			var bindAddressType = (SocksAddressType)await reader.ReadByteAsync(token); // ATYP

			if (bindAddressType == SocksAddressType.DomainName)
			{
				var addressLength = await reader.ReadByteAsync(token);
				await reader.ReadBytesAsync(addressLength, token); // Bind address
			}
			else if (bindAddressType == SocksAddressType.IPv4)
				await reader.ReadBytesAsync(4, token);
			else if (bindAddressType == SocksAddressType.IPv6)
				await reader.ReadBytesAsync(16, token);
			else
				throw new InvalidDataException("Unexpected bind address type");

			await reader.ReadBytesAsync(2, token); // Bind port
		}

		private static async Task WriteHandshakeMessage(AsyncBinaryWriter writer, CancellationToken token, params SocksAuthenticationType[] authenticationTypes)
		{
			await writer.WriteAsync(Socks5Version, token);
			await writer.WriteAsync((byte)authenticationTypes.Length, token);

			foreach (var authenticationType in authenticationTypes)
				await writer.WriteAsync((byte)authenticationType, token);
		}

		private static async Task WriteAuthenticationMessage(AsyncBinaryWriter writer, string username, string password, CancellationToken token)
		{
			byte[] usernameBytes = Encoding.UTF8.GetBytes(username);
			if (usernameBytes.Length > 255)
				throw new ArgumentOutOfRangeException(nameof(username), "Username is too long");

			byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
			if (passwordBytes.Length > 255)
				throw new ArgumentOutOfRangeException(nameof(password), "Password is too long");

			await writer.WriteAsync(AuthNegotiationVersion, token);
			 
			await writer.WriteAsync((byte)usernameBytes.Length, token);
			await writer.WriteAsync(usernameBytes, token);
			 
			await writer.WriteAsync((byte)passwordBytes.Length, token);
			await writer.WriteAsync(passwordBytes, token);
		}
	}
}