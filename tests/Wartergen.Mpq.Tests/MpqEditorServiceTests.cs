using Wartergen.Mpq;

namespace Wartergen.Mpq.Tests;

public class MpqEditorServiceTests
{
    private static string RealMpqEditorExePath => Path.Combine(AppContext.BaseDirectory, "MPQEditor", "MPQEditor.exe");

    [Fact]
    public async Task ExtractFileAsync_Throws_WhenExeDoesNotExist()
    {
        var service = new MpqEditorService(Path.Combine(AppContext.BaseDirectory, "does-not-exist.exe"));

        MpqOperationException ex = await Assert.ThrowsAsync<MpqOperationException>(
            () => service.ExtractFileAsync("map.w3x", "war3map.w3e", Path.GetTempPath()));

        Assert.Null(ex.ExitCode);
    }

    [Fact]
    public async Task ExtractFileAsync_ThrowsTimeoutError_WhenProcessDoesNotExitInTime()
    {
        // Uses the real bundled MPQEditor.exe with an intentionally tiny timeout so the test is
        // fast and deterministic regardless of whether the exe errors out quickly on the bogus
        // paths or opens a GUI window (the exact risk this wrapper needs to guard against).
        Assert.True(File.Exists(RealMpqEditorExePath), $"Expected MPQEditor.exe at {RealMpqEditorExePath}");

        var service = new MpqEditorService(RealMpqEditorExePath, TimeSpan.FromMilliseconds(1));

        MpqOperationException ex = await Assert.ThrowsAsync<MpqOperationException>(
            () => service.ExtractFileAsync(@"C:\does\not\exist.w3x", "war3map.w3e", Path.GetTempPath()));

        Assert.Contains("did not exit within", ex.Message);
    }

    [Fact]
    public async Task ExtractThenAdd_RoundTrips_AgainstRealSampleMap()
    {
        // Opt-in integration test: set WARTERGEN_TEST_MAP to a real .w3x/.w3m file path to run
        // this against a real archive. No-ops otherwise (no sample map ships in this repo).
        string? sampleMapPath = Environment.GetEnvironmentVariable("WARTERGEN_TEST_MAP");
        if (string.IsNullOrWhiteSpace(sampleMapPath) || !File.Exists(sampleMapPath))
        {
            return;
        }

        string scratchDir = Path.Combine(Path.GetTempPath(), $"wartergen-mpq-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(scratchDir);
        string scratchMap = Path.Combine(scratchDir, Path.GetFileName(sampleMapPath));
        File.Copy(sampleMapPath, scratchMap);

        try
        {
            var service = new MpqEditorService(RealMpqEditorExePath);
            string extractDir = Path.Combine(scratchDir, "extracted");

            await service.ExtractFileAsync(scratchMap, "war3map.w3e", extractDir);
            string extractedFile = Path.Combine(extractDir, "war3map.w3e");
            Assert.True(File.Exists(extractedFile));

            await service.AddOrReplaceFileAsync(scratchMap, extractedFile, "war3map.w3e");
        }
        finally
        {
            Directory.Delete(scratchDir, recursive: true);
        }
    }
}
