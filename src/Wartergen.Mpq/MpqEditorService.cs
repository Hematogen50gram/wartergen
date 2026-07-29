using System.Diagnostics;

namespace Wartergen.Mpq;

public sealed class MpqEditorService : IMpqEditorService
{
    private readonly string _exePath;
    private readonly TimeSpan _timeout;

    public MpqEditorService(string exePath, TimeSpan? timeout = null)
    {
        _exePath = exePath;
        _timeout = timeout ?? TimeSpan.FromSeconds(30);
    }

    // Resolves MPQEditor.exe relative to the app's base directory, matching the
    // CopyToOutputDirectory layout set up in Wartergen.App.csproj.
    public static MpqEditorService CreateDefault(TimeSpan? timeout = null)
    {
        string exePath = Path.Combine(AppContext.BaseDirectory, "MPQEditor", "MPQEditor.exe");
        return new MpqEditorService(exePath, timeout);
    }

    public async Task ExtractFileAsync(string mapPath, string fileNameInArchive, string outputDir, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDir);

        IReadOnlyList<string> args = MpqCommandArgs.Extract(mapPath, fileNameInArchive, outputDir);
        ProcessResult result = await RunProcessAsync(args, cancellationToken);

        string extractedPath = Path.Combine(outputDir, fileNameInArchive);
        if (!File.Exists(extractedPath))
        {
            throw new MpqOperationException(
                $"MPQEditor did not produce the expected extracted file '{fileNameInArchive}'.",
                result.CommandLine, result.ExitCode, result.StdOut, result.StdErr);
        }
    }

    public async Task AddOrReplaceFileAsync(string mapPath, string sourceFilePath, string targetNameInArchive, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> args = MpqCommandArgs.AddOrReplace(mapPath, sourceFilePath, targetNameInArchive);
        ProcessResult result = await RunProcessAsync(args, cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new MpqOperationException(
                $"MPQEditor exited with a non-zero code while adding '{targetNameInArchive}' to the archive.",
                result.CommandLine, result.ExitCode, result.StdOut, result.StdErr);
        }
    }

    private async Task<ProcessResult> RunProcessAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        if (!File.Exists(_exePath))
        {
            throw new MpqOperationException($"MPQEditor.exe was not found at '{_exePath}'.", _exePath, null, string.Empty, string.Empty);
        }

        string commandLine = $"\"{_exePath}\" {string.Join(' ', args.Select(a => $"\"{a}\""))}";

        var startInfo = new ProcessStartInfo
        {
            FileName = _exePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            throw new MpqOperationException("Failed to start MPQEditor.exe.", commandLine, null, string.Empty, string.Empty, ex);
        }

        Task<string> stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        using var timeoutCts = new CancellationTokenSource(_timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            await process.WaitForExitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            TryKillProcessTree(process);
            throw new MpqOperationException(
                $"MPQEditor did not exit within {_timeout.TotalSeconds:0}s. It may have opened a GUI window instead of running silently " +
                "— check that the command-line syntax matches what this version of MPQEditor expects.",
                commandLine, null, string.Empty, string.Empty);
        }

        string stdOut = await stdOutTask;
        string stdErr = await stdErrTask;

        return new ProcessResult(process.ExitCode, stdOut, stdErr, commandLine);
    }

    private static void TryKillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best-effort cleanup; nothing more we can do if this fails.
        }
    }

    private readonly record struct ProcessResult(int ExitCode, string StdOut, string StdErr, string CommandLine);
}
