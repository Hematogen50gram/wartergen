using Wartergen.Mpq;

namespace Wartergen.Mpq.Tests;

public class MpqCommandArgsTests
{
    [Fact]
    public void Extract_ProducesExpectedTokenOrder()
    {
        IReadOnlyList<string> args = MpqCommandArgs.Extract(@"C:\maps\my map.w3x", "war3map.w3e", @"C:\temp\out dir");

        Assert.Equal(
        [
            "extract",
            @"C:\maps\my map.w3x",
            "war3map.w3e",
            @"C:\temp\out dir",
            "/fp"
        ], args);
    }

    [Fact]
    public void AddOrReplace_ProducesExpectedTokenOrder()
    {
        IReadOnlyList<string> args = MpqCommandArgs.AddOrReplace(@"C:\maps\my map.w3x", @"C:\temp\war3map.w3e", "war3map.w3e");

        Assert.Equal(
        [
            "add",
            @"C:\maps\my map.w3x",
            @"C:\temp\war3map.w3e",
            "war3map.w3e"
        ], args);
    }
}
