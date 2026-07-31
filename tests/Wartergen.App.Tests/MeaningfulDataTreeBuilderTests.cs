using System.Text.Json;
using Wartergen.App.Models;

namespace Wartergen.App.Tests;

public class MeaningfulDataTreeBuilderTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void ArrayWithFewOutliers_KeepsOnlyDifferingIndices()
    {
        var node = MeaningfulDataTreeBuilder.CreateNode("groundTexture", Parse("[0,0,0,0,5,0,0,9,0,0,0,7,0]"));

        node.EnsureChildrenLoaded();

        Assert.Equal("[13 items, 3 differ from 0]", node.ValueSummary);
        Assert.Equal(["[4]", "[7]", "[11]"], node.Children.Select(c => c.Name));
        Assert.Equal(["5", "9", "7"], node.Children.Select(c => c.ValueSummary));
    }

    [Fact]
    public void ArrayWithAllEqualValues_HasNoChildren()
    {
        var node = MeaningfulDataTreeBuilder.CreateNode("flags", Parse("[1,1,1,1,1]"));

        Assert.Equal("[5 items, all equal to 1]", node.ValueSummary);
        Assert.False(node.HasChildren);
    }

    [Fact]
    public void EmptyArray_HasNoChildren()
    {
        var node = MeaningfulDataTreeBuilder.CreateNode("items", Parse("[]"));

        Assert.Equal("[0 items]", node.ValueSummary);
        Assert.False(node.HasChildren);
    }

    [Fact]
    public void BooleanArray_TreatsMinorityAsDiffering()
    {
        var node = MeaningfulDataTreeBuilder.CreateNode("boundaryFlag", Parse("[false,false,false,true,false]"));

        node.EnsureChildrenLoaded();

        Assert.Equal("[5 items, 1 differ from false]", node.ValueSummary);
        var only = Assert.Single(node.Children);
        Assert.Equal("[3]", only.Name);
        Assert.Equal("true", only.ValueSummary);
    }

    [Fact]
    public void NestedObject_RecursesIntoEachProperty()
    {
        var node = MeaningfulDataTreeBuilder.CreateNode("$", Parse("""{"map": {"width": 64}, "groundTexture": [0,0,0,3]}"""));

        node.EnsureChildrenLoaded();

        var map = node.Children.Single(c => c.Name == "map");
        map.EnsureChildrenLoaded();
        Assert.Equal("64", map.Children.Single(c => c.Name == "width").ValueSummary);

        var groundTexture = node.Children.Single(c => c.Name == "groundTexture");
        groundTexture.EnsureChildrenLoaded();
        var only = Assert.Single(groundTexture.Children);
        Assert.Equal("[3]", only.Name);
        Assert.Equal("3", only.ValueSummary);
    }

    [Fact]
    public void ArrayOfObjects_FallsBackToFullRepresentation()
    {
        var node = MeaningfulDataTreeBuilder.CreateNode("items", Parse("""[{"a":1},{"a":2}]"""));

        node.EnsureChildrenLoaded();

        Assert.Equal(2, node.Children.Count);
        Assert.Equal(["[0]", "[1]"], node.Children.Select(c => c.Name));
    }

    [Fact]
    public void ManyOutliers_AreGroupedLikeTheRegularBuilder()
    {
        var values = Enumerable.Range(0, 1200).Select(i => i % 3 == 0 ? "0" : i.ToString());
        var node = MeaningfulDataTreeBuilder.CreateNode("items", Parse("[" + string.Join(",", values) + "]"));

        node.EnsureChildrenLoaded();

        // 1200 values, 1/3 are the majority (0), so 800 differ -> ceil(800/500) = 2 groups.
        Assert.Equal(2, node.Children.Count);
        Assert.All(node.Children, c => Assert.Equal(JsonNodeKind.Group, c.Kind));
    }

    [Fact]
    public void RealTerrainFixture_GroundTextureShowsOnlyDifferingTiles()
    {
        var json = File.ReadAllText(Path.Combine("TestData", "terrain.json"));
        var node = MeaningfulDataTreeBuilder.CreateNode("$", Parse(json));

        node.EnsureChildrenLoaded();

        var groundTexture = node.Children.Single(c => c.Name == "groundTexture");
        Assert.Equal("[4225 items, 243 differ from 0]", groundTexture.ValueSummary);
    }
}
