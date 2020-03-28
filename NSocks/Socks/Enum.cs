namespace NSocks.Socks
{
	/// <summary>
	/// The command to the Socks5 server.
	/// </summary>
	public enum SocksCommand : byte
	{
		/// <summary>
		/// Emulates a HTTP Connect request, and acts as a proxy for a single request.
		/// </summary>
		Connect = 1,

		/// <summary>
		/// Creates a listener port on the Socks5 server to relay TCP requests back to the client.
		/// </summary>
		Bind = 2,

		/// <summary>
		/// Opens a UDP port on the Socks5 server, and acts as a relay point between the destination and the client.
		/// </summary>
		UdpAssociate = 3
	}

	/// <summary>
	/// The type of authentication used for the Socks5 server negotiation.
	/// </summary>
	public enum SocksAuthenticationType : byte
	{
		/// <summary>
		/// No authentication is used for the request, or as anonymous.
		/// </summary>
		NoAuthentication = 0,

		/// <summary>
		/// GSSAPI / Kerberos based authentication.
		/// </summary>
		GSSAPI = 1,

		/// <summary>
		/// Plain username + password authentication.
		/// </summary>
		UsernamePassword = 2
	}

	public enum SocksAddressType : byte
	{
		IPv4 = 1,
		DomainName = 3,
		IPv6 = 4
	}

	/// <summary>
	/// The type of reply from the Socks5 server when negotiating a connection.
	/// </summary>
	public enum SocksRequestReplyType : byte
	{
		/// <summary>
		/// The connection succeeded.
		/// </summary>
		Succeeded = 0,

		/// <summary>
		/// Internal unspecified error happened in the proxy server software.
		/// </summary>
		GeneralSocksServerFailure = 1,

		/// <summary>
		/// The server is not allowed to make a connection to the host.
		/// </summary>
		ConnectionNotAllowedByRuleset = 2,

		/// <summary>
		/// The server cannot connect to the IP address.
		/// </summary>
		NetworkUnreachable = 3,

		/// <summary>
		/// The host cannot be resolved, or cannot be found.
		/// </summary>
		HostUnreachable = 4,

		/// <summary>
		/// The destination host refused to open a connection to the proxy.
		/// </summary>
		ConnectionRefused = 5,

		/// <summary>
		/// The TTL property of the TCP packet had reached it's expiration limit.
		/// </summary>
		TtlExpired = 6,

		/// <summary>
		/// The requested command is not supported.
		/// </summary>
		CommandNotSupported = 7,

		/// <summary>
		/// The specified address type of the host is not supported.
		/// </summary>
		AddressTypeNotSupported = 8
	}
}