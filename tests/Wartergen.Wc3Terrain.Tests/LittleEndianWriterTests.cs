using Wartergen.Wc3Terrain;

namespace Wartergen.Wc3Terrain.Tests;

public class LittleEndianWriterTests
{
    [Fact]
    public void AddInt_WritesFourBytesLittleEndian()
    {
        var writer = new LittleEndianWriter();
        writer.AddInt(12); // W3E! version

        Assert.Equal(new byte[] { 0x0C, 0x00, 0x00, 0x00 }, writer.GetBuffer());
    }

    [Fact]
    public void AddShort_WritesTwoBytesLittleEndian_ForPositiveValue()
    {
        var writer = new LittleEndianWriter();
        writer.AddShort(8192);

        Assert.Equal(new byte[] { 0x00, 0x20 }, writer.GetBuffer());
    }

    [Fact]
    public void AddShort_HandlesValuesAboveInt16Max_ForPackedFlagsGroundTexture()
    {
        // flags (upper 10 bits, can include bit 15) OR'd with groundTexture (lower 6 bits)
        // can legitimately exceed short.MaxValue (32767) up to ushort.MaxValue (65535).
        var writer = new LittleEndianWriter();
        writer.AddShort(65535);

        Assert.Equal(new byte[] { 0xFF, 0xFF }, writer.GetBuffer());
    }

    [Fact]
    public void AddShort_HandlesNegativeValues_TwosComplement()
    {
        var writer = new LittleEndianWriter();
        writer.AddShort(-100);

        // -100 as 16-bit two's complement is 0xFF9C, little-endian bytes: 9C FF
        Assert.Equal(new byte[] { 0x9C, 0xFF }, writer.GetBuffer());
    }

    [Fact]
    public void AddByte_MasksToLowByte()
    {
        var writer = new LittleEndianWriter();
        writer.AddByte(0xF8 | 0x07); // groundVariation | cliffVariation, max packed byte value

        Assert.Equal(new byte[] { 0xFF }, writer.GetBuffer());
    }

    [Fact]
    public void AddChars_WritesOneByePerCharacter()
    {
        var writer = new LittleEndianWriter();
        writer.AddChars("W3E!");

        Assert.Equal(new byte[] { (byte)'W', (byte)'3', (byte)'E', (byte)'!' }, writer.GetBuffer());
    }

    [Fact]
    public void AddFloat_WritesFourBytesLittleEndian()
    {
        var writer = new LittleEndianWriter();
        writer.AddFloat(-4096f);

        byte[] expected = BitConverter.GetBytes(-4096f);
        if (!BitConverter.IsLittleEndian) Array.Reverse(expected);

        Assert.Equal(expected, writer.GetBuffer());
    }
}
