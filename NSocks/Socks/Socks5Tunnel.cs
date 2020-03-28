using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;

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
		public static void PerformHandshake(Stream networkStream, NetworkCredential credentials = null)
		{
			using var reader = new BinaryReader(networkStream, Encoding.ASCII, true);
			using var writer = new BinaryWriter(networkStream, Encoding.ASCII, true);

			// Send handshake message

			var authenticationMethods = credentials != null
				? new[] { SocksAuthenticationType.NoAuthentication, SocksAuthenticationType.UsernamePassword }
				: new[] { SocksAuthenticationType.NoAuthentication };

			WriteHandshakeMessage(writer, authenticationMethods);
			writer.Flush();

			// Receive handshake response

			byte[] handshakeBuffer = new byte[2];

			if (reader.Read(handshakeBuffer) != 2)
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

					WriteAuthenticationMessage(writer, credentials.UserName, credentials.Password);
					writer.Flush();

					if (reader.Read(handshakeBuffer) != 2)
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
		public static void SendConnectRequest(Stream networkStream, Uri destinationUri, bool resolveDnsLocally = true)
		{
			using var reader = new BinaryReader(networkStream, Encoding.ASCII, true);
			using var writer = new BinaryWriter(networkStream, Encoding.ASCII, true);

			writer.Write(Socks5Version);
			writer.Write((byte)SocksCommand.Connect);
			writer.Write((byte)0); // Reserved

			if (!IPAddress.TryParse(destinationUri.Host, out var ipAddress))
			{
				if (resolveDnsLocally)
				{
					ipAddress = Dns.GetHostAddresses(destinationUri.Host).FirstOrDefault();

					if (ipAddress == null)
						throw new ArgumentException("Unable to resolve host", nameof(destinationUri));
				}
			}

			if (ipAddress != null)
			{
				var addressType = ipAddress.AddressFamily == AddressFamily.InterNetworkV6
					? SocksAddressType.IPv6
					: SocksAddressType.IPv4;

				writer.Write((byte)addressType);
				writer.Write(ipAddress.GetAddressBytes());
			}
			else
			{
				writer.Write((byte)SocksAddressType.DomainName);

				byte[] addressNameBytes = Encoding.ASCII.GetBytes(destinationUri.Host);

				writer.Write((byte)addressNameBytes.Length);
				writer.Write(addressNameBytes);
			}

			// We can't use the regular BinaryWriter.Write(uint) method here as it writes in little endian, when we need the port number written as big endian.
			// Plus this makes it more consistent across runtime implementations.

			writer.Write((byte)(destinationUri.Port >> 8));
			writer.Write((byte)(destinationUri.Port & 0xFF));

			writer.Flush();

			if (reader.ReadByte() != Socks5Version)
				throw new InvalidDataException("Proxy response was invalid");

			var replyType = (SocksRequestReplyType)reader.ReadByte();

			switch (replyType)
			{
				case SocksRequestReplyType.Succeeded:
					break;

				default:
					throw new Exception($"Socks server responded with error code: {replyType}");
			}

			// Since we are using the CONNECT command here, we do not need to parse the last part of the packet as they're only used for the BIND command
			// Just read them to empty the network buffer

			reader.ReadByte(); // Reserved byte
			var bindAddressType = (SocksAddressType)reader.ReadByte(); // ATYP

			if (bindAddressType == SocksAddressType.DomainName)
			{
				var addressLength = reader.ReadByte();
				reader.ReadBytes(addressLength); // Bind address
			}
			else if (bindAddressType == SocksAddressType.IPv4)
				reader.ReadBytes(4);
			else if (bindAddressType == SocksAddressType.IPv6)
				reader.ReadBytes(16);

			reader.ReadBytes(2); // Bind port
		}

		private static void WriteHandshakeMessage(BinaryWriter writer, params SocksAuthenticationType[] authenticationTypes)
		{
			writer.Write(Socks5Version);
			writer.Write((byte)authenticationTypes.Length);

			foreach (var authenticationType in authenticationTypes)
				writer.Write((byte)authenticationType);
		}

		private static void WriteAuthenticationMessage(BinaryWriter writer, string username, string password)
		{
			byte[] usernameBytes = Encoding.UTF8.GetBytes(username);
			if (usernameBytes.Length > 255)
				throw new ArgumentOutOfRangeException(nameof(username), "Username is too long");

			byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
			if (passwordBytes.Length > 255)
				throw new ArgumentOutOfRangeException(nameof(password), "Password is too long");

			writer.Write(AuthNegotiationVersion);

			writer.Write((byte)usernameBytes.Length);
			writer.Write(usernameBytes);

			writer.Write((byte)passwordBytes.Length);
			writer.Write(passwordBytes);
		}
	}
}