using System.Text;

namespace Wartergen.Mpq;

public sealed class MpqOperationException : Exception
{
    public int? ExitCode { get; }
    public string StandardOutput { get; }
    public string StandardError { get; }
    public string CommandLine { get; }

    public MpqOperationException(
        string message,
        string commandLine,
        int? exitCode,
        string standardOutput,
        string standardError,
        Exception? innerException = null)
        : base(BuildMessage(message, commandLine, exitCode, standardOutput, standardError), innerException)
    {
        CommandLine = commandLine;
        ExitCode = exitCode;
        StandardOutput = standardOutput;
        StandardError = standardError;
    }

    private static string BuildMessage(string message, string commandLine, int? exitCode, string standardOutput, string standardError)
    {
        var sb = new StringBuilder();
        sb.AppendLine(message);
        sb.AppendLine($"Command: {commandLine}");
        if (exitCode.HasValue)
        {
            sb.AppendLine($"Exit code: {exitCode.Value}");
        }
        if (!string.IsNullOrWhiteSpace(standardOutput))
        {
            sb.AppendLine($"stdout: {standardOutput.Trim()}");
        }
        if (!string.IsNullOrWhiteSpace(standardError))
        {
            sb.AppendLine($"stderr: {standardError.Trim()}");
        }
        return sb.ToString();
    }
}
