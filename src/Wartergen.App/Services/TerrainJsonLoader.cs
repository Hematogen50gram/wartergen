using System.IO;
using System.Text.Json;
using Wartergen.Mpq;
using Wartergen.Wc3Terrain;

namespace Wartergen.App.Services;

public sealed class TerrainJsonLoader
{
    private const string TerrainFileName = "war3map.w3e";

    private readonly IMpqEditorService mpqEditorService;

    public TerrainJsonLoader(IMpqEditorService mpqEditorService)
    {
        this.mpqEditorService = mpqEditorService;
    }

    public async Task<JsonDocument> LoadAsync(string mapPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(mapPath))
        {
            throw new FileNotFoundException($"Map file not found: {mapPath}", mapPath);
        }

        string tempDir = Path.Combine(Path.GetTempPath(), "Wartergen", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            await mpqEditorService.ExtractFileAsync(mapPath, TerrainFileName, tempDir, cancellationToken);

            byte[] warBytes = await File.ReadAllBytesAsync(Path.Combine(tempDir, TerrainFileName), cancellationToken);
            TerrainModel terrain = TerrainTranslator.WarToJson(warBytes);

            return JsonSerializer.SerializeToDocument(terrain);
        }
        finally
        {
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch
            {
                // Best-effort cleanup only; don't fail the overall operation on cleanup errors.
            }
        }
    }
}
