using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NSocks
{
	public class AsyncBinaryWriter : AsyncBinaryBase
	{
		public AsyncBinaryWriter(Stream stream) : base(stream)
		{
		}

		public AsyncBinaryWriter(Stream stream, Encoding encoding) : base(stream, encoding)
		{
		}

		public AsyncBinaryWriter(Stream stream, Encoding encoding, bool leaveOpen) : base(stream, encoding, leaveOpen)
		{
		}

		public Task WriteAsync(byte[] byteArray, CancellationToken token = default)
		{
			return BaseStream.WriteAsync(byteArray, 0, byteArray.Length, token);
		}

		public Task WriteAsync(byte value, CancellationToken token = default)
		{
			return BaseStream.WriteAsync(new [] { value } , 0, 1, token);
		}
	}
}
