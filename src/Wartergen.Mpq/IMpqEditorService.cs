namespace Wartergen.Mpq;

public interface IMpqEditorService
{
    // Extracts fileNameInArchive from mapPath into outputDir. Success is verified by checking
    // the extracted file exists on disk afterward (MPQEditor's exit code/stdout format for this
    // operation is not reliably documented).
    Task ExtractFileAsync(string mapPath, string fileNameInArchive, string outputDir, CancellationToken cancellationToken = default);

    // Adds or replaces targetNameInArchive inside mapPath with the contents of sourceFilePath.
    // Success is verified via exit code (best-effort).
    Task AddOrReplaceFileAsync(string mapPath, string sourceFilePath, string targetNameInArchive, CancellationToken cancellationToken = default);
}
