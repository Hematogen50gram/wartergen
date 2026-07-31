namespace Wartergen.App.Models;

internal static class JsonTreeGrouping
{
    public static IReadOnlyList<JsonTreeNode> BuildChildren(int count, Func<int, int, IReadOnlyList<JsonTreeNode>> buildRange)
    {
        if (count <= JsonTreeBuilder.DirectChildThreshold)
        {
            return buildRange(0, count);
        }

        var groups = new List<JsonTreeNode>();
        for (var start = 0; start < count; start += JsonTreeBuilder.ChunkSize)
        {
            var rangeStart = start;
            var rangeLength = Math.Min(JsonTreeBuilder.ChunkSize, count - start);
            var label = rangeLength == 1
                ? $"[{rangeStart}]"
                : $"[{rangeStart}–{rangeStart + rangeLength - 1}]";

            groups.Add(new JsonTreeNode(label, JsonNodeKind.Group, string.Empty, hasChildren: true,
                childFactory: () => buildRange(rangeStart, rangeLength)));
        }
        return groups;
    }
}
