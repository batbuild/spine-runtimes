using System;
using System.IO;
using System.Text;

namespace Spine
{
	public class BigEndianBinaryReader : BinaryReader
	{
		private char[] _chars = new char[32];
		private byte[] _a32 = new byte[4];
		private byte[] _a16 = new byte[2];

		public BigEndianBinaryReader(Stream input)
			: base(input)
		{
		}

		public BigEndianBinaryReader(Stream input, Encoding encoding)
			: base(input, encoding)
		{
		}

		public int ReadInt(bool optimizePositive)
		{
			int b = ReadByte();
			var result = b & 0x7F;
			if ((b & 0x80) == 0) return optimizePositive ? result : ((result >> 1) ^ -(result & 1));

			b = ReadByte();
			result |= (b & 0x7F) << 7;
			if ((b & 0x80) == 0) return optimizePositive ? result : ((result >> 1) ^ -(result & 1));

			b = ReadByte();
			result |= (b & 0x7F) << 14;
			if ((b & 0x80) == 0) return optimizePositive ? result : ((result >> 1) ^ -(result & 1));

			b = ReadByte();
			result |= (b & 0x7F) << 21;
			if ((b & 0x80) == 0) return optimizePositive ? result : ((result >> 1) ^ -(result & 1));

			b = ReadByte();
			result |= (b & 0x7F) << 28;
			return optimizePositive ? result : ((result >> 1) ^ -(result & 1));
		}

		public override string ReadString()
		{
			var charCount = ReadInt(true);
			switch (charCount)
			{
				case 0:
					return null;
				case 1:
					return "";
			}
			charCount--;

			if (_chars.Length < charCount) _chars = new char[charCount];
			var chars = _chars;
			// Try to read 7 bit ASCII chars.
			var charIndex = 0;
			var b = 0;
			while (charIndex < charCount)
			{
				b = ReadByte();
				if (b > 127) break;
				chars[charIndex++] = (char)b;
			}
			// If a char was not ASCII, finish with slow path.
			if (charIndex < charCount) readUtf8_slow(charCount, charIndex, b);
			return new String(chars, 0, charCount);
		}

		public override float ReadSingle()
		{
			_a32 = ReadBytes(4);
			Array.Reverse(_a32);
			return BitConverter.ToSingle(_a32, 0);
		}

		public override short ReadInt16()
		{
			_a16 = ReadBytes(2);
			Array.Reverse(_a16);
			return BitConverter.ToInt16(_a16, 0);
		}

		public override int ReadInt32()
		{
			_a32 = ReadBytes(4);
			Array.Reverse(_a32);
			return BitConverter.ToInt32(_a32, 0);
		}

		private void readUtf8_slow(int charCount, int charIndex, int b)
		{
			var chars = _chars;
			while (true)
			{
				switch (b >> 4)
				{
					case 0:
					case 1:
					case 2:
					case 3:
					case 4:
					case 5:
					case 6:
					case 7:
						chars[charIndex] = (char)b;
						break;
					case 12:
					case 13:
						chars[charIndex] = (char)((b & 0x1F) << 6 | ReadByte() & 0x3F);
						break;
					case 14:
						chars[charIndex] = (char)((b & 0x0F) << 12 | (ReadByte() & 0x3F) << 6 | ReadByte() & 0x3F);
						break;
				}
				if (++charIndex >= charCount) break;
				b = ReadByte() & 0xFF;
			}
		}
	}
}