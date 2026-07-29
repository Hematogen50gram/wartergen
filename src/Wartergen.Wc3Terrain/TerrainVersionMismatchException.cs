namespace Wartergen.Wc3Terrain;

public sealed class TerrainVersionMismatchException : Exception
{
    public int ExpectedVersion { get; }
    public int FoundVersion { get; }

    public TerrainVersionMismatchException(int expectedVersion, int foundVersion)
        : base("Wartergen cannot currently parse this version of a war3map.w3e file")
    {
        ExpectedVersion = expectedVersion;
        FoundVersion = foundVersion;
    }

    internal static void ExpectVersion(int expected, int actual)
    {
        if (actual != expected)
        {
            throw new TerrainVersionMismatchException(expected, actual);
        }
    }
}
