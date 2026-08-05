using System.Text.Json;
using Wartergen.Mpq;
using Wartergen.Wc3Terrain;

namespace Wartergen.App.Services;

public sealed class TerrainJsonLoader
{
    private readonly IMpqEditorService mpqEditorService;

    public TerrainJsonLoader(IMpqEditorService mpqEditorService)
    {
        this.mpqEditorService = mpqEditorService;
    }

    public async Task<JsonDocument> LoadAsync(string mapPath, CancellationToken cancellationToken = default)
    {
        TerrainModel terrain = await TerrainMapExtractor.ExtractTerrainModelAsync(mpqEditorService, mapPath, cancellationToken);
        return JsonSerializer.SerializeToDocument(terrain);
    }
}
