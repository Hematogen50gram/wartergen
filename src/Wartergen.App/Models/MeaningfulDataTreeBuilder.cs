using System.Text.Json;

namespace Wartergen.App.Models;

// Builds a second, filtered view of the same document: for arrays of scalar values
// (the per-tile terrain arrays are the motivating case), only the elements that differ
// from the array's most common ("majority") value are kept, alongside their original index.
// This surfaces the handful of tiles that were actually painted differently instead of
// scrolling through thousands of repeated defaults.
public static class MeaningfulDataTreeBuilder
{
    public static JsonTreeNode CreateNode(string name, JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => CreateObjectNode(name, element),
        JsonValueKind.Array => CreateArrayNode(name, element),
        _ => JsonTreeBuilder.CreateNode(name, element)
    };

    private static JsonTreeNode CreateObjectNode(string name, JsonElement element)
    {
        var count = element.EnumerateObject().Count();
        var summary = $"{{{count} propert{(count == 1 ? "y" : "ies")}}}";

        if (count == 0)
        {
            return new JsonTreeNode(name, JsonNodeKind.Object, summary, hasChildren: false);
        }

        return new JsonTreeNode(name, JsonNodeKind.Object, summary, hasChildren: true,
            childFactory: () => JsonTreeGrouping.BuildChildren(count, (start, length) =>
                element.EnumerateObject()
                    .Skip(start)
                    .Take(length)
                    .Select(property => CreateNode(property.Name, property.Value))
                    .ToList()));
    }

    private static JsonTreeNode CreateArrayNode(string name, JsonElement element)
    {
        var length = element.GetArrayLength();

        if (length == 0)
        {
            return new JsonTreeNode(name, JsonNodeKind.Array, "[0 items]", hasChildren: false);
        }

        var entries = element.EnumerateArray()
            .Select((value, index) => (Index: index, Value: value))
            .ToList();

        if (!entries.All(e => IsScalar(e.Value.ValueKind)))
        {
            // No well-defined "majority value" for arrays containing objects/arrays —
            // fall back to the full representation rather than guessing at a comparison.
            return JsonTreeBuilder.CreateNode(name, element);
        }

        var mode = entries
            .GroupBy(e => e.Value.GetRawText())
            .OrderByDescending(g => g.Count())
            .First();

        var differing = entries.Where(e => e.Value.GetRawText() != mode.Key).ToList();

        var summary = differing.Count == 0
            ? $"[{length} items, all equal to {mode.Key}]"
            : $"[{length} items, {differing.Count} differ from {mode.Key}]";

        if (differing.Count == 0)
        {
            return new JsonTreeNode(name, JsonNodeKind.Array, summary, hasChildren: false);
        }

        return new JsonTreeNode(name, JsonNodeKind.Array, summary, hasChildren: true,
            childFactory: () => JsonTreeGrouping.BuildChildren(differing.Count, (start, count) =>
                differing.Skip(start).Take(count)
                    .Select(e => JsonTreeBuilder.CreateNode($"[{e.Index}]", e.Value))
                    .ToList()));
    }

    private static bool IsScalar(JsonValueKind kind) =>
        kind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null;
}
