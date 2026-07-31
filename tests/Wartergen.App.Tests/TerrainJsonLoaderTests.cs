using Wartergen.App.Services;
using Wartergen.Mpq;

namespace Wartergen.App.Tests;

public class TerrainJsonLoaderTests
{
    private sealed class FakeMpqEditorService(string sourceFilePath) : IMpqEditorService
    {
        public Task ExtractFileAsync(string mapPath, string fileNameInArchive, string outputDir, CancellationToken cancellationToken = default)
        {
            Directory.CreateDirectory(outputDir);
            File.Copy(sourceFilePath, Path.Combine(outputDir, fileNameInArchive), overwrite: true);
            return Task.CompletedTask;
        }

        public Task AddOrReplaceFileAsync(string mapPath, string sourceFilePath, string targetNameInArchive, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not used by the terrain preview feature.");
    }

    [Fact]
    public async Task LoadAsync_ExtractsAndTranslatesTerrainToJson()
    {
        string mapPath = Path.GetTempFileName();
        try
        {
            var loader = new TerrainJsonLoader(new FakeMpqEditorService(Path.Combine("TestData", "war3map.w3e")));

            using var document = await loader.LoadAsync(mapPath);

            Assert.Equal("L", document.RootElement.GetProperty("tileset").GetString());
            Assert.Equal(64, document.RootElement.GetProperty("map").GetProperty("width").GetInt32());
            Assert.Equal(64, document.RootElement.GetProperty("map").GetProperty("height").GetInt32());
            Assert.True(document.RootElement.GetProperty("groundHeight").GetArrayLength() > 0);
        }
        finally
        {
            File.Delete(mapPath);
        }
    }

    [Fact]
    public async Task LoadAsync_MapFileMissing_ThrowsFileNotFoundException()
    {
        var loader = new TerrainJsonLoader(new FakeMpqEditorService(Path.Combine("TestData", "war3map.w3e")));
        string missingMapPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.w3x");

        await Assert.ThrowsAsync<FileNotFoundException>(() => loader.LoadAsync(missingMapPath));
    }
}
