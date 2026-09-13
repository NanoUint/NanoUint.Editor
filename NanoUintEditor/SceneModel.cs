using System.Collections.ObjectModel;
using System.Windows;

namespace NanoUintEditor;

// DATA MODEL (extracted from MainWindow.xaml.cs; independently testable)

public class ComponentData
{
    public string Type { get; set; } = "";
    public Dictionary<string, object?> Props { get; set; } = new();
}

public class SceneObject
{
    private double _x;
    private double _y;
    private double _width = 100;
    private double _height = 100;
    private double _opacity = 1.0;
    private int _sortingOrder;

    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..6];
    public string Name { get; set; } = "GameObject";
    public string? ParentId { get; set; }  // null = root

    // Properties are the single source of truth; setters sync the Transform component's Props (avoids dual-storage drift)
    public double X { get => _x; set { _x = value; SyncTransform("X", value); } }
    public double Y { get => _y; set { _y = value; SyncTransform("Y", value); } }
    public double Width { get => _width; set { _width = value; SyncTransform("Width", value); } }
    public double Height { get => _height; set { _height = value; SyncTransform("Height", value); } }
    public double Opacity { get => _opacity; set { _opacity = value; SyncTransform("Opacity", value); } }
    public int SortingOrder { get => _sortingOrder; set { _sortingOrder = value; SyncTransform("SortingOrder", (double)value); } }

    public List<ComponentData> Components { get; set; } = new();
    public FrameworkElement? Visual { get; set; }

    private void SyncTransform(string key, object value)
    {
        var t = Components.FirstOrDefault(c => c.Type == "Transform");
        if (t != null) t.Props[key] = value;
    }

    public ComponentData? GetComponent(string type) => Components.FirstOrDefault(c => c.Type == type);
    public bool HasComponent(string type) => Components.Any(c => c.Type == type);
}

/// <summary>Pure hierarchy logic (world position/cycle guard/cascade/validation). UI-independent and unit-testable.</summary>
public static class SceneModel
{
    public static double WorldX(IReadOnlyList<SceneObject> objects, SceneObject obj)
    {
        double x = 0;
        var seen = new HashSet<string>();
        var cur = obj;
        while (cur != null)
        {
            if (!seen.Add(cur.Id)) break; // Cycle guard: stop at a revisited node so positions are not double-counted
            x += cur.X;
            if (cur.ParentId == null) break;
            cur = objects.FirstOrDefault(o => o.Id == cur.ParentId);
        }
        return x;
    }

    public static double WorldY(IReadOnlyList<SceneObject> objects, SceneObject obj)
    {
        double y = 0;
        var seen = new HashSet<string>();
        var cur = obj;
        while (cur != null)
        {
            if (!seen.Add(cur.Id)) break; // Cycle guard: stop at a revisited node so positions are not double-counted
            y += cur.Y;
            if (cur.ParentId == null) break;
            cur = objects.FirstOrDefault(o => o.Id == cur.ParentId);
        }
        return y;
    }

    public static IEnumerable<SceneObject> GetChildren(IReadOnlyList<SceneObject> objects, string parentId) =>
        objects.Where(o => o.ParentId == parentId);

    /// <summary>Whether ancestorId is an ancestor of possibleChildId (prevents making an object its own descendant).</summary>
    public static bool IsDescendant(IReadOnlyList<SceneObject> objects, string ancestorId, string possibleChildId)
    {
        var cur = objects.FirstOrDefault(o => o.Id == possibleChildId);
        while (cur != null && cur.ParentId != null)
        {
            if (cur.ParentId == ancestorId) return true;
            cur = objects.FirstOrDefault(o => o.Id == cur.ParentId);
        }
        return false;
    }

    /// <summary>Validates the hierarchy after load: fixes dangling ParentId and parent-child cycles (both become roots).</summary>
    public static void ValidateHierarchy(IReadOnlyList<SceneObject> objects)
    {
        foreach (var o in objects)
        {
            if (o.ParentId == null) continue;
            var parent = objects.FirstOrDefault(p => p.Id == o.ParentId);
            if (parent == null) { o.ParentId = null; continue; }
            var seen = new HashSet<string> { o.Id };
            var cur = parent;
            while (cur != null)
            {
                if (!seen.Add(cur.Id)) { o.ParentId = null; break; }
                cur = cur.ParentId != null ? objects.FirstOrDefault(p => p.Id == cur.ParentId) : null;
            }
        }
    }

    public static void CollectDescendants(IReadOnlyList<SceneObject> objects, SceneObject obj, List<SceneObject> list)
    {
        foreach (var child in GetChildren(objects, obj.Id).ToList())
        {
            list.Add(child);
            CollectDescendants(objects, child, list);
        }
    }

    /// <summary>Deep-copies the object list (without Visual; used for undo/redo snapshots and duplication).</summary>
    public static List<SceneObject> CloneObjects(IEnumerable<SceneObject> objects) =>
        objects.Select(o => new SceneObject
        {
            Id = o.Id,
            Name = o.Name,
            ParentId = o.ParentId,
            X = o.X,
            Y = o.Y,
            Width = o.Width,
            Height = o.Height,
            Opacity = o.Opacity,
            SortingOrder = o.SortingOrder,
            Components = o.Components.Select(c => new ComponentData { Type = c.Type, Props = new(c.Props) }).ToList(),
        }).ToList();
}
