using System.Text;

namespace Wartergen.Wc3Terrain;

public sealed class LittleEndianWriter
{
    private readonly List<byte> _buffer = new();

    public void AddString(string str)
    {
        foreach (byte b in Encoding.UTF8.GetBytes(str))
        {
            _buffer.Add(b);
        }
        AddNullTerminator();
    }

    public void AddNewLine()
    {
        _buffer.Add(0x0d); // carriage return
        _buffer.Add(0x0a); // line feed
    }

    public void AddChar(char character)
    {
        _buffer.Add((byte)character);
    }

    public void AddChars(string chars)
    {
        foreach (char character in chars)
        {
            AddChar(character);
        }
    }

    public void AddInt(int value)
    {
        _buffer.Add((byte)(value & 0xFF));
        _buffer.Add((byte)((value >> 8) & 0xFF));
        _buffer.Add((byte)((value >> 16) & 0xFF));
        _buffer.Add((byte)((value >> 24) & 0xFF));
    }

    public void AddInt24(int value)
    {
        _buffer.Add((byte)(value & 0xFF));
        _buffer.Add((byte)((value >> 8) & 0xFF));
        _buffer.Add((byte)((value >> 16) & 0xFF));
    }

    public void AddShort(int value)
    {
        _buffer.Add((byte)(value & 0xFF));
        _buffer.Add((byte)((value >> 8) & 0xFF));
    }

    public void AddFloat(float value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (!BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }
        _buffer.AddRange(bytes);
    }

    public void AddByte(int value)
    {
        _buffer.Add((byte)(value & 0xFF));
    }

    public void AddNullTerminator()
    {
        _buffer.Add(0x00);
    }

    public byte[] GetBuffer() => _buffer.ToArray();
}
