namespace Wartergen.Wc3Terrain;

public sealed class LittleEndianReader
{
    private readonly byte[] _buffer;
    private int _offset;

    public LittleEndianReader(byte[] buffer)
    {
        _buffer = buffer;
    }

    public int ReadInt()
    {
        int value = _buffer[_offset]
            | (_buffer[_offset + 1] << 8)
            | (_buffer[_offset + 2] << 16)
            | (_buffer[_offset + 3] << 24);
        _offset += 4;
        return value;
    }

    public int ReadInt24()
    {
        int value = _buffer[_offset]
            | (_buffer[_offset + 1] << 8)
            | (_buffer[_offset + 2] << 16);
        _offset += 3;

        // Sign-extend from bit 23.
        if ((value & 0x800000) != 0)
        {
            value |= unchecked((int)0xFF000000);
        }

        return value;
    }

    public int ReadShort()
    {
        int value = _buffer[_offset] | (_buffer[_offset + 1] << 8);
        _offset += 2;

        // Reinterpret the low 16 bits as signed, sign-extending to int (matches Buffer.readInt16LE).
        return (short)value;
    }

    public float ReadFloat()
    {
        byte[] bytes = new byte[4];
        Array.Copy(_buffer, _offset, bytes, 0, 4);
        if (!BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }
        _offset += 4;

        float value = BitConverter.ToSingle(bytes, 0);
        return (float)Math.Round(value, 3, MidpointRounding.AwayFromZero);
    }

    public string ReadString()
    {
        var chars = new List<byte>();
        while (_buffer[_offset] != 0x00)
        {
            chars.Add(_buffer[_offset]);
            _offset += 1;
        }
        _offset += 1; // consume the \0 end-of-string delimiter

        return string.Concat(chars.Select(b => (char)b));
    }

    public string ReadChars(int len = 1, bool allowNull = false)
    {
        int numCharsToRead = len == 0 ? 1 : len;
        var chars = new char[numCharsToRead];

        for (int i = 0; i < numCharsToRead; i++)
        {
            byte b = _buffer[_offset];
            _offset += 1;
            chars[i] = (!allowNull && b == 0x0) ? '0' : (char)b;
        }

        return new string(chars);
    }

    public string ReadFourCC() => ReadChars(4, true);

    public byte ReadByte()
    {
        byte value = _buffer[_offset];
        _offset += 1;
        return value;
    }

    public bool IsExhausted() => _offset == _buffer.Length;
}
