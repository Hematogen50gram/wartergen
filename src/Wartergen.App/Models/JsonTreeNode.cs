using System.Collections.ObjectModel;

namespace Wartergen.App.Models;

public enum JsonNodeKind
{
    Object,
    Array,
    String,
    Number,
    Boolean,
    Null,
    Group
}

public sealed class JsonTreeNode
{
    private static readonly JsonTreeNode Placeholder = new("Loading...", JsonNodeKind.Null, "", hasChildren: false);

    private Func<IReadOnlyList<JsonTreeNode>>? childFactory;
    private bool childrenLoaded;

    public string Name { get; }
    public JsonNodeKind Kind { get; }
    public string ValueSummary { get; }
    public string DisplayText { get; }
    public bool HasChildren { get; }
    public ObservableCollection<JsonTreeNode> Children { get; } = new();

    public JsonTreeNode(string name, JsonNodeKind kind, string valueSummary, bool hasChildren, Func<IReadOnlyList<JsonTreeNode>>? childFactory = null)
    {
        Name = name;
        Kind = kind;
        ValueSummary = valueSummary;
        HasChildren = hasChildren;
        DisplayText = kind == JsonNodeKind.Group ? name : $"{name}: {valueSummary}";
        this.childFactory = childFactory;

        if (hasChildren)
        {
            Children.Add(Placeholder);
        }
    }

    public void EnsureChildrenLoaded()
    {
        if (childrenLoaded || childFactory is null)
        {
            return;
        }

        childrenLoaded = true;
        var factory = childFactory;
        childFactory = null;

        Children.Clear();
        foreach (var child in factory())
        {
            Children.Add(child);
        }
    }
}
