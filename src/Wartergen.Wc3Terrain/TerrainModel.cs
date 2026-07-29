using System.Text.Json.Serialization;

namespace Wartergen.Wc3Terrain;

public sealed class TerrainModel
{
    [JsonPropertyName("tileset")]
    public string Tileset { get; set; } = string.Empty;

    [JsonPropertyName("customTileset")]
    public bool CustomTileset { get; set; }

    [JsonPropertyName("tilePalette")]
    public string[] TilePalette { get; set; } = [];

    [JsonPropertyName("cliffTilePalette")]
    public string[] CliffTilePalette { get; set; } = [];

    [JsonPropertyName("map")]
    public TerrainMap Map { get; set; } = new();

    [JsonPropertyName("groundHeight")]
    public int[] GroundHeight { get; set; } = [];

    [JsonPropertyName("waterHeight")]
    public int[] WaterHeight { get; set; } = [];

    [JsonPropertyName("boundaryFlag")]
    public bool[] BoundaryFlag { get; set; } = [];

    [JsonPropertyName("flags")]
    public int[] Flags { get; set; } = [];

    [JsonPropertyName("groundTexture")]
    public int[] GroundTexture { get; set; } = [];

    [JsonPropertyName("groundVariation")]
    public int[] GroundVariation { get; set; } = [];

    [JsonPropertyName("cliffVariation")]
    public int[] CliffVariation { get; set; } = [];

    [JsonPropertyName("cliffTexture")]
    public int[] CliffTexture { get; set; } = [];

    [JsonPropertyName("layerHeight")]
    public int[] LayerHeight { get; set; } = [];
}

public sealed class TerrainMap
{
    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("offset")]
    public TerrainOffset Offset { get; set; } = new();
}

public sealed class TerrainOffset
{
    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }
}
