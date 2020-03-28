using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NSocks
{
	/// <summary>
	/// Combines multiple underlying streams into a single stream.
	/// </summary>
	internal class MultiPartStream : Stream
	{
		protected IEnumerator<Stream> Enumerator;

		protected Stream CurrentBaseStream { get; set; }
		protected bool SafeMoveNext()
		{
			if (Enumerator.MoveNext())
			{
				CurrentBaseStream?.Dispose();
				CurrentBaseStream = Enumerator.Current;

				return true;
			}
			else
			{
				CurrentBaseStream = null;
				return false;
			}
		}

		public MultiPartStream(IEnumerable<Stream> streams, long? totalLength = null)
		{
			Length = totalLength ?? -1;

			Enumerator = streams.GetEnumerator();
			SafeMoveNext();
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			if (CurrentBaseStream == null)
				return 0;

			int copiedCount = 0;

			do
			{
				copiedCount += CurrentBaseStream.Read(buffer, offset + copiedCount, count - copiedCount);
			} while (copiedCount < count && SafeMoveNext());

			_position += copiedCount;

			return copiedCount;
		}

		public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			if (CurrentBaseStream == null)
				return 0;

			int copiedCount = 0;

			do
			{
				copiedCount += await CurrentBaseStream.ReadAsync(buffer, offset + copiedCount, count - copiedCount, cancellationToken);
			} while (copiedCount < count && SafeMoveNext());

			_position += copiedCount;

			return copiedCount;
		}

		public override int Read(Span<byte> buffer)
		{
			if (CurrentBaseStream == null)
				return 0;

			int copiedCount = 0;
			var currentSpan = buffer;

			do
			{
				copiedCount += CurrentBaseStream.Read(currentSpan);
				currentSpan = buffer.Slice(copiedCount);
			} while (copiedCount < buffer.Length && SafeMoveNext());

			_position += copiedCount;

			return copiedCount;
		}

		public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = new CancellationToken())
		{
			if (CurrentBaseStream == null)
				return 0;

			int copiedCount = 0;
			var currentSpan = buffer;

			do
			{
				copiedCount += await CurrentBaseStream.ReadAsync(currentSpan, cancellationToken);
				currentSpan = buffer.Slice(copiedCount);
			} while (copiedCount < buffer.Length && SafeMoveNext());

			_position += copiedCount;

			return copiedCount;
		}

		public override void CopyTo(Stream destination, int bufferSize)
		{
			if (CurrentBaseStream == null)
				return;

			do
				CurrentBaseStream.CopyTo(destination, bufferSize);
			while (SafeMoveNext());
		}

		public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
		{
			if (CurrentBaseStream == null)
				return;

			do
				await CurrentBaseStream.CopyToAsync(destination, bufferSize, cancellationToken);
			while (SafeMoveNext());
		}

		public override void Flush() => throw new NotSupportedException();
		public override void SetLength(long value) => throw new NotSupportedException();
		public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

		public override bool CanRead { get; } = true;
		public override bool CanSeek { get; } = false;
		public override bool CanWrite { get; } = false;
		public override long Length { get; }

		private long _position;

		public override long Position
		{
			get => _position;
			set => throw new NotSupportedException();
		}

		public override void Close()
		{
			base.Close();

			CurrentBaseStream?.Dispose();
			Enumerator.Dispose();
		}
	}
}