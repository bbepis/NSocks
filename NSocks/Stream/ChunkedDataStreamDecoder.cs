using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace NSocks
{
	public static class ChunkedDataStreamDecoder
	{
		public static Stream GetStream(Stream inputStream)
		{
			return new MultiPartStream(GetChunkedStreams(inputStream));
		}

		private static IEnumerable<Stream> GetChunkedStreams(Stream inputStream)
		{
			using var reader = new BinaryReader(inputStream, Encoding.ASCII, true);

			while (true)
			{
				long length = 0;

				while (true)
				{
					char c = reader.ReadChar();

					switch (c)
					{
						default:
							length = (length * 16) + HexCharToInt(c);
							continue;
						case '\r':
							continue;
						case '\n':
							break;
					}

					break;
				}

				if (length == 0)
					yield break;

				yield return new LimitedStream(inputStream, length, true);

				reader.ReadBytes(2);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static int HexCharToInt(char ch)
		{
			if (ch >= 48 && ch <= 57)
				return ch - 48;

			if (ch >= 65 && ch <= 70)
				return ch - 55;

			if (ch >= 97 && ch <= 102)
				return ch - 87;

			throw new Exception("HexCharToInt: input out of range for Hex value");
		}
	}
}
