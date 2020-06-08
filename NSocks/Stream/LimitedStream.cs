using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NSocks
{
	public class LimitedStream : Stream
	{
		private long _position;

		public override bool CanRead => true;
		public override bool CanSeek => true;
		public override bool CanWrite => false;
		public override long Length => InternalLength;

		public override long Position
		{
			get => _position;
			set => Seek(value, SeekOrigin.Begin);
		}

		protected long InternalLength { get; set; }
		protected bool LeaveOpen { get; set; }

		protected Stream BaseStream { get; set; }

		public LimitedStream(Stream baseStream, long length, bool leaveOpen)
		{
			InternalLength = length;
			BaseStream = baseStream;
			LeaveOpen = leaveOpen;
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			if (Position >= Length)
				return 0;

			int read = (int)Math.Min(count, Length - Position);

			_position += read;

			return BaseStream.Read(buffer, offset, read);
		}

		public override int Read(Span<byte> buffer)
		{
			if (Position >= Length)
				return 0;

			int read = (int)Math.Min(buffer.Length, Length - Position);

			_position += read;

			return BaseStream.Read(buffer.Slice(0, read));
		}

		public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			if (Position >= Length)
				return Task.FromResult(0);

			int read = (int)Math.Min(count, Length - Position);

			_position += read;

			return BaseStream.ReadAsync(buffer, offset, read, cancellationToken);
		}

		public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = new CancellationToken())
		{
			if (Position >= Length)
				return new ValueTask<int>(0);

			int read = (int)Math.Min(buffer.Length, Length - Position);

			_position += read;

			return BaseStream.ReadAsync(buffer.Slice(0, read), cancellationToken);
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			long target = origin switch
			{
				SeekOrigin.Begin   => offset,
				SeekOrigin.Current => Position + offset,
				SeekOrigin.End     => Length + offset,
				_                  => throw new ArgumentOutOfRangeException(nameof(origin), origin, null)
			};

			if (target > Length || target < 0)
				throw new ArgumentOutOfRangeException(nameof(offset));

			_position = target;

			return target;
		}

		public override void SetLength(long value)
		{
			throw new NotSupportedException();
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			throw new NotSupportedException();
		}

		public override void Flush()
		{
			throw new NotSupportedException();
		}

		public override ValueTask DisposeAsync()
		{
			if (LeaveOpen)
				return new ValueTask(Task.CompletedTask);

			return BaseStream.DisposeAsync();
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				if (!LeaveOpen)
					BaseStream.Dispose();
			}
		}
	}
}