using System.IO;
using System.Text.Json;
using Wartergen.Bmp;
using Wartergen.Mpq;
using Wartergen.Wc3Terrain;

namespace Wartergen.App.Services;

public sealed class DrawTerrainWorkflow
{
    private const string TerrainFileName = "war3map.w3e";

    private readonly IMpqEditorService _mpqEditorService;

    public DrawTerrainWorkflow(IMpqEditorService mpqEditorService)
    {
        _mpqEditorService = mpqEditorService;
    }

    public async Task RunAsync(string mapPath, string bmpPath, string? cliffBmpPath, string? waterPngPath, string? heightsPngPath, IProgress<string> log, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(mapPath))
        {
            throw new DrawTerrainWorkflowException($"Map file not found: {mapPath}");
        }
        if (!File.Exists(bmpPath))
        {
            throw new DrawTerrainWorkflowException($"BMP file not found: {bmpPath}");
        }
        if (cliffBmpPath is not null && !File.Exists(cliffBmpPath))
        {
            throw new DrawTerrainWorkflowException($"Cliff BMP file not found: {cliffBmpPath}");
        }
        if (waterPngPath is not null && !File.Exists(waterPngPath))
        {
            throw new DrawTerrainWorkflowException($"Water PNG file not found: {waterPngPath}");
        }
        if (heightsPngPath is not null && !File.Exists(heightsPngPath))
        {
            throw new DrawTerrainWorkflowException($"Heights PNG file not found: {heightsPngPath}");
        }

        string tempDir = Path.Combine(Path.GetTempPath(), "Wartergen", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string terrainWarPath = Path.Combine(tempDir, TerrainFileName);

            log.Report($"Extracting {TerrainFileName} from the map...");
            try
            {
                await _mpqEditorService.ExtractFileAsync(mapPath, TerrainFileName, tempDir, cancellationToken);
            }
            catch (Exception ex)
            {
                throw new DrawTerrainWorkflowException($"Step 1 (extract {TerrainFileName}): {ex.Message}", ex);
            }

            log.Report($"Converting {TerrainFileName} to terrain.json...");
            TerrainModel terrain;
            try
            {
                byte[] warBytes = await File.ReadAllBytesAsync(terrainWarPath, cancellationToken);
                terrain = TerrainTranslator.WarToJson(warBytes);

                // Breadcrumb only, for debuggability — the app never reads this file back.
                string terrainJsonPath = Path.Combine(tempDir, "terrain.json");
                string terrainJson = JsonSerializer.Serialize(terrain, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(terrainJsonPath, terrainJson, cancellationToken);
            }
            catch (TerrainVersionMismatchException ex)
            {
                throw new DrawTerrainWorkflowException(
                    $"Step 2 (convert {TerrainFileName} to terrain.json): unsupported war3map.w3e version " +
                    $"(expected {ex.ExpectedVersion}, found {ex.FoundVersion}).", ex);
            }
            catch (Exception ex)
            {
                throw new DrawTerrainWorkflowException($"Step 2 (convert {TerrainFileName} to terrain.json): {ex.Message}", ex);
            }

            log.Report("Applying BMP colors to ground texture...");
            try
            {
                RgbColor[] pixels = BmpGroundTextureConverter.ReadPixelsTopLeftFirst(bmpPath);
                int[] convertedTexture = BmpGroundTextureConverter.BuildConvertedTexture(pixels);
                BmpGroundTextureConverter.ApplyGroundTexture(terrain.GroundTexture, convertedTexture);

                log.Report($"Applied BMP ({pixels.Length} pixels, {convertedTexture.Distinct().Count()} unique colors).");
            }
            catch (Exception ex)
            {
                throw new DrawTerrainWorkflowException($"Step 3 (apply BMP to ground texture): {ex.Message}", ex);
            }

            log.Report(cliffBmpPath is null
                ? "No cliff image provided — leaving cliffs unchanged..."
                : "Applying cliff BMP colors to cliff texture/layer height...");
            try
            {
                if (cliffBmpPath is not null)
                {
                    RgbColor[] pixels = BmpGroundTextureConverter.ReadPixelsTopLeftFirst(cliffBmpPath);
                    // Corner arrays are (width+1) x (height+1) per tile-cell grid (see
                    // TerrainTranslator's rowSize = Map.Width + 1), not Map.Width itself.
                    CliffBmpConverter.Apply(terrain.CliffTexture, terrain.LayerHeight, pixels, terrain.Map.Width + 1);

                    log.Report($"Applied cliff BMP ({pixels.Length} pixels).");
                }
            }
            catch (Exception ex)
            {
                throw new DrawTerrainWorkflowException($"Step 4 (apply cliff BMP): {ex.Message}", ex);
            }

            log.Report(waterPngPath is null
                ? "No water image provided — leaving water unchanged..."
                : "Applying water PNG colors to water height...");
            try
            {
                if (waterPngPath is not null)
                {
                    RgbaColor[] pixels = PngPixelReader.ReadPixelsTopLeftFirst(waterPngPath);
                    WaterPngConverter.Apply(terrain.WaterHeight, terrain.Flags, pixels);

                    log.Report($"Applied water PNG ({pixels.Length} pixels).");
                }
            }
            catch (Exception ex)
            {
                throw new DrawTerrainWorkflowException($"Step 5 (apply water PNG): {ex.Message}", ex);
            }

            log.Report(heightsPngPath is null
                ? "No heights image provided — leaving ground heights unchanged..."
                : "Applying heights PNG to ground height...");
            try
            {
                if (heightsPngPath is not null)
                {
                    RgbaColor[] pixels = PngPixelReader.ReadPixelsTopLeftFirst(heightsPngPath);
                    HeightsPngConverter.Apply(terrain.GroundHeight, pixels);

                    log.Report($"Applied heights PNG ({pixels.Length} pixels).");
                }
            }
            catch (Exception ex)
            {
                throw new DrawTerrainWorkflowException($"Step 6 (apply heights PNG): {ex.Message}", ex);
            }

            log.Report($"Converting terrain.json back to {TerrainFileName}...");
            try
            {
                byte[] warBytes = TerrainTranslator.JsonToWar(terrain);
                await File.WriteAllBytesAsync(terrainWarPath, warBytes, cancellationToken);
            }
            catch (Exception ex)
            {
                throw new DrawTerrainWorkflowException($"Step 7 (convert terrain.json to {TerrainFileName}): {ex.Message}", ex);
            }

            log.Report($"Repacking {TerrainFileName} into the map...");
            try
            {
                await _mpqEditorService.AddOrReplaceFileAsync(mapPath, terrainWarPath, TerrainFileName, cancellationToken);
            }
            catch (Exception ex)
            {
                throw new DrawTerrainWorkflowException($"Step 8 (repack {TerrainFileName}): {ex.Message}", ex);
            }

            log.Report("Done.");
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
