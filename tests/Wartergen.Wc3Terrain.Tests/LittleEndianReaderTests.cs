using Wartergen.Wc3Terrain;

namespace Wartergen.Wc3Terrain.Tests;

public class LittleEndianReaderTests
{
    [Fact]
    public void ReadInt_ReadsFourBytesLittleEndian()
    {
        var reader = new LittleEndianReader([0x0C, 0x00, 0x00, 0x00]);
        Assert.Equal(12, reader.ReadInt());
    }

    [Fact]
    public void ReadShort_ReadsPositiveValue()
    {
        var reader = new LittleEndianReader([0x00, 0x20]);
        Assert.Equal(8192, reader.ReadShort());
    }

    [Fact]
    public void ReadShort_SignExtendsNegativeValue()
    {
        var reader = new LittleEndianReader([0x9C, 0xFF]);
        Assert.Equal(-100, reader.ReadShort());
    }

    [Fact]
    public void ReadByte_AdvancesOffsetByOne()
    {
        var reader = new LittleEndianReader([0x2A, 0x2B]);
        Assert.Equal(0x2A, reader.ReadByte());
        Assert.Equal(0x2B, reader.ReadByte());
        Assert.True(reader.IsExhausted());
    }

    [Fact]
    public void ReadChars_ReplacesNullByteWithZeroCharacter_UnlessAllowNull()
    {
        var reader = new LittleEndianReader([0x00, (byte)'A']);
        Assert.Equal("0A", reader.ReadChars(2));
    }

    [Fact]
    public void ReadChars_AllowsNullByte_WhenAllowNullTrue()
    {
        var reader = new LittleEndianReader([0x00, (byte)'A']);
        Assert.Equal("\0A", reader.ReadChars(2, allowNull: true));
    }

    [Fact]
    public void ReadFourCC_ReadsFourCharsAllowingNull()
    {
        var reader = new LittleEndianReader([(byte)'L', (byte)'d', (byte)'r', (byte)'t']);
        Assert.Equal("Ldrt", reader.ReadFourCC());
    }

    [Theory]
    [InlineData((ushort)0b1111_1111_1100_0000, 0b1111_1111_1100_0000, 0)] // flags fills all bits (incl. bit 15), groundTexture = 0
    [InlineData((ushort)0b0000_0000_0011_1111, 0, 0b0000_0000_0011_1111)] // groundTexture fills lower 6 bits, flags = 0
    public void FlagsAndGroundTexture_BitPacking_MatchesMaskExpressions(ushort raw, int expectedFlags, int expectedGroundTexture)
    {
        var reader = new LittleEndianReader([(byte)(raw & 0xFF), (byte)((raw >> 8) & 0xFF)]);
        int flagsAndGroundTexture = reader.ReadShort();

        int flags = flagsAndGroundTexture & 0b1111_1111_1100_0000;
        int groundTexture = flagsAndGroundTexture & 0b0000_0000_0011_1111;

        Assert.Equal(expectedFlags, flags);
        Assert.Equal(expectedGroundTexture, groundTexture);
    }

    [Fact]
    public void WaterHeightAndBoundaryFlag_BitPacking_NoBoundary_PassesWaterHeightThrough()
    {
        // waterHeight below bit 14 (0x4000) so it doesn't collide with the boundary flag bit.
        int raw = 12345;
        var reader = new LittleEndianReader([(byte)(raw & 0xFF), (byte)((raw >> 8) & 0xFF)]);

        int waterHeightAndBoundary = reader.ReadShort();
        int waterHeight = waterHeightAndBoundary & 32767;
        bool boundaryFlag = (waterHeightAndBoundary & 0x4000) == 0x4000;

        Assert.Equal(12345, waterHeight);
        Assert.False(boundaryFlag);
    }

    [Fact]
    public void WaterHeightAndBoundaryFlag_BitPacking_BoundarySet_IsFoldedIntoWaterHeightMask()
    {
        // The waterHeight mask (0x7FFF) includes bit 14, the same bit the boundary flag lives in,
        // so when the flag is set it is also reflected in the masked waterHeight value (matches
        // the original TerrainTranslator.ts behavior verbatim, not independently maskable).
        int raw = 12345 | 0x4000;
        var reader = new LittleEndianReader([(byte)(raw & 0xFF), (byte)((raw >> 8) & 0xFF)]);

        int waterHeightAndBoundary = reader.ReadShort();
        int waterHeight = waterHeightAndBoundary & 32767;
        bool boundaryFlag = (waterHeightAndBoundary & 0x4000) == 0x4000;

        Assert.Equal(12345 | 0x4000, waterHeight);
        Assert.True(boundaryFlag);
    }

    [Fact]
    public void GroundAndCliffVariation_BitPacking_ExtractsBoth()
    {
        int raw = 0b11011000 | 0b00000101; // groundVariation upper 5, cliffVariation lower 3
        var reader = new LittleEndianReader([(byte)raw]);

        int packed = reader.ReadByte();
        int groundVariation = packed & 0b11111000;
        int cliffVariation = packed & 0b00000111;

        Assert.Equal(0b11011000, groundVariation);
        Assert.Equal(0b00000101, cliffVariation);
    }

    [Fact]
    public void CliffTextureAndLayerHeight_BitPacking_ExtractsBoth()
    {
        int raw = 0b10100000 | 0b00001001; // cliffTexture upper 4, layerHeight lower 4
        var reader = new LittleEndianReader([(byte)raw]);

        int packed = reader.ReadByte();
        int cliffTexture = packed & 0b11110000;
        int layerHeight = packed & 0b00001111;

        Assert.Equal(0b10100000, cliffTexture);
        Assert.Equal(0b00001001, layerHeight);
    }
}
