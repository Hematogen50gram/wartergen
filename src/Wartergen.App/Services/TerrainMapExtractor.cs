using System.IO;
using Wartergen.Mpq;
using Wartergen.Wc3Terrain;

namespace Wartergen.App.Services;

// Shared "extract war3map.w3e from a map and translate it to a TerrainModel" step, used by the
// full terrain-JSON preview (TerrainJsonLoader), which doesn't need the BMP/repack steps that
// DrawTerrainWorkflow also performs.
internal static class TerrainMapExtractor
{
    private const string TerrainFileName = "war3map.w3e";

    public static async Task<TerrainModel> ExtractTerrainModelAsync(IMpqEditorService mpqEditorService, string mapPath, CancellationToken cancellationToken = default)
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
            return TerrainTranslator.WarToJson(warBytes);
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
