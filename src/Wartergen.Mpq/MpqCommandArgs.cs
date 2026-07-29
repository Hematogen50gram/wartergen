namespace Wartergen.Mpq;

// Pure argument-list construction, kept separate from process invocation so it's directly testable.
public static class MpqCommandArgs
{
    public static IReadOnlyList<string> Extract(string mapPath, string fileNameInArchive, string outputDir) =>
        [ "extract", mapPath, fileNameInArchive, outputDir, "/fp" ];

    public static IReadOnlyList<string> AddOrReplace(string mapPath, string sourceFilePath, string targetNameInArchive) =>
        [ "add", mapPath, sourceFilePath, targetNameInArchive ];
}
