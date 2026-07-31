using System.Text.Json;

namespace Wartergen.App.Models;

public static class JsonTreeBuilder
{
    public const int DirectChildThreshold = 100;
    public const int ChunkSize = 500;
    private const int MaxStringLength = 200;

    public static JsonTreeNode CreateNode(string name, JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => CreateObjectNode(name, element),
        JsonValueKind.Array => CreateArrayNode(name, element),
        JsonValueKind.String => CreateScalarNode(name, JsonNodeKind.String, FormatString(element)),
        JsonValueKind.Number => CreateScalarNode(name, JsonNodeKind.Number, element.GetRawText()),
        JsonValueKind.True or JsonValueKind.False => CreateScalarNode(name, JsonNodeKind.Boolean, element.GetRawText()),
        _ => CreateScalarNode(name, JsonNodeKind.Null, "null")
    };

    private static JsonTreeNode CreateScalarNode(string name, JsonNodeKind kind, string valueSummary) =>
        new(name, kind, valueSummary, hasChildren: false);

    private static string FormatString(JsonElement element)
    {
        var value = element.GetString() ?? string.Empty;
        var truncated = value.Length > MaxStringLength ? value[..MaxStringLength] + "..." : value;
        return $"\"{truncated}\"";
    }

    private static JsonTreeNode CreateObjectNode(string name, JsonElement element)
    {
        var count = element.EnumerateObject().Count();
        var summary = $"{{{count} propert{(count == 1 ? "y" : "ies")}}}";

        if (count == 0)
        {
            return new JsonTreeNode(name, JsonNodeKind.Object, summary, hasChildren: false);
        }

        return new JsonTreeNode(name, JsonNodeKind.Object, summary, hasChildren: true,
            childFactory: () => BuildObjectChildren(element, count));
    }

    private static IReadOnlyList<JsonTreeNode> BuildObjectChildren(JsonElement element, int count) =>
        JsonTreeGrouping.BuildChildren(count, (start, length) =>
            element.EnumerateObject()
                .Skip(start)
                .Take(length)
                .Select(property => CreateNode(property.Name, property.Value))
                .ToList());

    private static JsonTreeNode CreateArrayNode(string name, JsonElement element)
    {
        var count = element.GetArrayLength();
        var summary = $"[{count} item{(count == 1 ? "" : "s")}]";

        if (count == 0)
        {
            return new JsonTreeNode(name, JsonNodeKind.Array, summary, hasChildren: false);
        }

        return new JsonTreeNode(name, JsonNodeKind.Array, summary, hasChildren: true,
            childFactory: () => BuildArrayChildren(element, count));
    }

    private static IReadOnlyList<JsonTreeNode> BuildArrayChildren(JsonElement element, int count) =>
        JsonTreeGrouping.BuildChildren(count, (start, length) =>
            element.EnumerateArray()
                .Skip(start)
                .Take(length)
                .Select((item, offset) => CreateNode($"[{start + offset}]", item))
                .ToList());
}
