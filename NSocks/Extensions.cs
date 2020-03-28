using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NSocks
{
	public static class Extensions
	{
		/// <summary>
		/// Connects the socket to the specified IP address and port.
		/// </summary>
		/// <param name="socket">The socket to connect.</param>
		/// <param name="ipAddress">The destination IP address.</param>
		/// <param name="port">The destination port.</param>
		public static Task ConnectAsync(this Socket socket, IPAddress ipAddress, int port)
		{
			return Task.Factory.FromAsync(
				(callback, state) => socket.BeginConnect(ipAddress, port, callback, state),
				socket.EndConnect,
				null);
		}
	}
}