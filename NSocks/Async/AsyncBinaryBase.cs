using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NSocks
{
	public abstract class AsyncBinaryBase : IDisposable, IAsyncDisposable
	{
		public Stream BaseStream { get; protected set; }
		public Encoding Encoding { get; protected set; }

		protected bool LeaveOpen { get; set; }

		protected AsyncBinaryBase(Stream stream) : this(stream, Encoding.Default)
		{
		}

		protected AsyncBinaryBase(Stream stream, Encoding encoding) : this(stream, encoding, false)
		{
		}

		protected AsyncBinaryBase(Stream stream, Encoding encoding, bool leaveOpen)
		{
			BaseStream = stream;

			Encoding = encoding;
			LeaveOpen = leaveOpen;
		}

		public void Flush()
		{
			BaseStream.Flush();
		}

		public Task FlushAsync(CancellationToken token = default)
		{
			return BaseStream.FlushAsync(token);
		}

		public async ValueTask DisposeAsync()
		{
			await FlushAsync();

			if (!LeaveOpen)
			{
				await BaseStream.DisposeAsync();
			}
		}

		public virtual void Dispose()
		{
			Flush();

			if (!LeaveOpen)
			{
				BaseStream.Dispose();
			}
		}
	}
}