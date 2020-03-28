using System;
using System.Net.Http;
using System.Threading.Tasks;
using NUnit.Framework;

namespace NSocks.Test
{
	public class Socks5HandlerTests
	{
		[Test]
		public async Task BasicConnectTest()
		{
			var proxyUri = new Uri("socks5://127.0.0.1:1080/");

			using var httpClient = new HttpClient(new Socks5Handler(proxyUri));
			
			await httpClient.GetAsync("https://www.ietf.org/rfc/rfc1928.txt");
		}
	}
}