using System.Text.Json;
using Wartergen.App.Models;

namespace Wartergen.App.Tests;

public class JsonTreeBuilderTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void EmptyObject_HasNoChildren()
    {
        var node = JsonTreeBuilder.CreateNode("root", Parse("{}"));

        Assert.Equal(JsonNodeKind.Object, node.Kind);
        Assert.Equal("{0 properties}", node.ValueSummary);
        Assert.False(node.HasChildren);
        Assert.Empty(node.Children);
    }

    [Fact]
    public void EmptyArray_HasNoChildren()
    {
        var node = JsonTreeBuilder.CreateNode("root", Parse("[]"));

        Assert.Equal(JsonNodeKind.Array, node.Kind);
        Assert.Equal("[0 items]", node.ValueSummary);
        Assert.False(node.HasChildren);
        Assert.Empty(node.Children);
    }

    [Fact]
    public void Null_IsLeafNode()
    {
        var node = JsonTreeBuilder.CreateNode("root", Parse("null"));

        Assert.Equal(JsonNodeKind.Null, node.Kind);
        Assert.Equal("null", node.ValueSummary);
        Assert.False(node.HasChildren);
    }

    [Fact]
    public void NestedObject_PreservesChildNamesAndOrder()
    {
        var node = JsonTreeBuilder.CreateNode("map", Parse("""{"width": 64, "height": 64, "offset": {"x": -4096, "y": -4096}}"""));

        node.EnsureChildrenLoaded();
        Assert.Equal(["width", "height", "offset"], node.Children.Select(c => c.Name));

        var offset = node.Children.Single(c => c.Name == "offset");
        offset.EnsureChildrenLoaded();
        Assert.Equal(["x", "y"], offset.Children.Select(c => c.Name));
    }

    [Fact]
    public void SmallArray_ProducesDirectChildrenWithNoGrouping()
    {
        var node = JsonTreeBuilder.CreateNode("items", Parse("[10, 20, 30, 40, 50]"));

        node.EnsureChildrenLoaded();

        Assert.Equal(5, node.Children.Count);
        Assert.All(node.Children, c => Assert.NotEqual(JsonNodeKind.Group, c.Kind));
        Assert.Equal(["[0]", "[1]", "[2]", "[3]", "[4]"], node.Children.Select(c => c.Name));
    }

    [Fact]
    public void ArrayAtThreshold_HasNoGrouping()
    {
        var json = "[" + string.Join(",", Enumerable.Range(0, JsonTreeBuilder.DirectChildThreshold)) + "]";
        var node = JsonTreeBuilder.CreateNode("items", Parse(json));

        node.EnsureChildrenLoaded();

        Assert.Equal(JsonTreeBuilder.DirectChildThreshold, node.Children.Count);
        Assert.All(node.Children, c => Assert.NotEqual(JsonNodeKind.Group, c.Kind));
    }

    [Fact]
    public void ArrayJustOverThreshold_ProducesOneGroup()
    {
        var count = JsonTreeBuilder.DirectChildThreshold + 1;
        var json = "[" + string.Join(",", Enumerable.Range(0, count)) + "]";
        var node = JsonTreeBuilder.CreateNode("items", Parse(json));

        node.EnsureChildrenLoaded();

        var group = Assert.Single(node.Children);
        Assert.Equal(JsonNodeKind.Group, group.Kind);

        group.EnsureChildrenLoaded();
        Assert.Equal(count, group.Children.Count);
    }

    [Fact]
    public void LargeArray_ChunksIntoGroupsOfExpectedSize()
    {
        const int count = 1200;
        var json = "[" + string.Join(",", Enumerable.Range(0, count)) + "]";
        var node = JsonTreeBuilder.CreateNode("items", Parse(json));

        node.EnsureChildrenLoaded();

        Assert.Equal(3, node.Children.Count);
        Assert.All(node.Children, c => Assert.Equal(JsonNodeKind.Group, c.Kind));

        var groups = node.Children.ToList();
        Assert.Equal("[0–499]", groups[0].Name);
        Assert.Equal("[500–999]", groups[1].Name);
        Assert.Equal("[1000–1199]", groups[2].Name);

        groups[0].EnsureChildrenLoaded();
        groups[1].EnsureChildrenLoaded();
        groups[2].EnsureChildrenLoaded();

        Assert.Equal(500, groups[0].Children.Count);
        Assert.Equal(500, groups[1].Children.Count);
        Assert.Equal(200, groups[2].Children.Count);

        Assert.Equal("[1000]", groups[2].Children[0].Name);
        Assert.Equal("1000", groups[2].Children[0].ValueSummary);
    }

    [Fact]
    public void LargeNode_DoesNotEagerlyMaterializeGrandchildren()
    {
        const int count = 5000;
        var json = "[" + string.Join(",", Enumerable.Range(0, count)) + "]";
        var node = JsonTreeBuilder.CreateNode("items", Parse(json));

        // Creating the node must not walk into any group's children.
        Assert.Single(node.Children);
        Assert.Equal("Loading...", node.Children[0].Name);

        node.EnsureChildrenLoaded();

        foreach (var group in node.Children)
        {
            Assert.Equal(JsonNodeKind.Group, group.Kind);
            var placeholder = Assert.Single(group.Children);
            Assert.Equal("Loading...", placeholder.Name);
        }
    }

    [Fact]
    public void RealTerrainFixture_RootPropertiesAndGroundHeightChunking()
    {
        var json = File.ReadAllText(Path.Combine("TestData", "terrain.json"));
        var node = JsonTreeBuilder.CreateNode("$", Parse(json));

        node.EnsureChildrenLoaded();

        Assert.Equal(
            ["tileset", "customTileset", "tilePalette", "cliffTilePalette", "map",
             "groundHeight", "waterHeight", "boundaryFlag", "flags", "groundTexture",
             "groundVariation", "cliffVariation", "cliffTexture", "layerHeight"],
            node.Children.Select(c => c.Name));

        var groundHeight = node.Children.Single(c => c.Name == "groundHeight");
        groundHeight.EnsureChildrenLoaded();

        // 4225 elements -> ceil(4225 / 500) = 9 groups.
        Assert.Equal(9, groundHeight.Children.Count);

        var lastGroup = groundHeight.Children[^1];
        lastGroup.EnsureChildrenLoaded();
        Assert.Equal(225, lastGroup.Children.Count);
    }
}
