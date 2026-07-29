using System.Text.Json;
using Wartergen.Wc3Terrain;

namespace Wartergen.Wc3Terrain.Tests;

public class RoundTripOracleTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static string TestDataPath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "TestData", fileName);

    [Fact]
    public void JsonToWarToJson_ProducesEquivalentModel()
    {
        string json = File.ReadAllText(TestDataPath("terrain.json"));
        var original = JsonSerializer.Deserialize<TerrainModel>(json)
            ?? throw new InvalidOperationException("Failed to deserialize terrain.json fixture");

        byte[] warBytes = TerrainTranslator.JsonToWar(original);
        var roundTripped = TerrainTranslator.WarToJson(warBytes);

        string originalJson = JsonSerializer.Serialize(original, JsonOptions);
        string roundTrippedJson = JsonSerializer.Serialize(roundTripped, JsonOptions);

        Assert.Equal(originalJson, roundTrippedJson);
    }

    [Fact]
    public void WarToJsonToWar_ProducesByteIdenticalOutput()
    {
        byte[] originalBytes = File.ReadAllBytes(TestDataPath("war3map.w3e"));

        var terrain = TerrainTranslator.WarToJson(originalBytes);
        byte[] roundTrippedBytes = TerrainTranslator.JsonToWar(terrain);

        Assert.Equal(originalBytes, roundTrippedBytes);
    }

    [Fact]
    public void WarToJson_ThrowsOnUnsupportedVersion()
    {
        byte[] originalBytes = File.ReadAllBytes(TestDataPath("war3map.w3e"));
        byte[] corrupted = (byte[])originalBytes.Clone();
        // Version int32 lives right after the 4-byte "W3E!" header; corrupt it.
        corrupted[4] = 0xFF;

        var ex = Assert.Throws<TerrainVersionMismatchException>(() => TerrainTranslator.WarToJson(corrupted));
        Assert.Equal(12, ex.ExpectedVersion);
    }
}
