using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NSocks
{
	public class AsyncBinaryReader : AsyncBinaryBase
	{
		public AsyncBinaryReader(Stream stream) : base(stream)
		{
		}

		public AsyncBinaryReader(Stream stream, Encoding encoding) : base(stream, encoding)
		{
		}

		public AsyncBinaryReader(Stream stream, Encoding encoding, bool leaveOpen) : base(stream, encoding, leaveOpen)
		{
		}

		public Task<int> ReadAsync(byte[] byteArray, CancellationToken token = default)
		{
			return BaseStream.ReadAsync(byteArray, 0, byteArray.Length, token);
		}

		public async Task<byte> ReadByteAsync(CancellationToken token = default)
		{
			byte[] buffer = new byte[1];
			var result = await BaseStream.ReadAsync(buffer, 0, 1, token);

			if (result == -1)
				throw new EndOfStreamException();

			return buffer[0];
		}

		public async Task<byte[]> ReadBytesAsync(int length, CancellationToken token = default)
		{
			byte[] buffer = new byte[length];
			int count = 0;

			while (count < length)
			{
				var result = await BaseStream.ReadAsync(buffer, count, length - count, token);

				if (result == -1)
					throw new EndOfStreamException();

				count += result;
			}

			return buffer;
		}
	}
}
