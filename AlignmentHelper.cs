namespace NanoUintEditor;

/// <summary>Alignment mode for selected elements.</summary>
public enum AlignMode
{
    Left, CenterH, Right,
    Top, CenterV, Bottom,
    DistributeH, DistributeV
}

/// <summary>Operates on a selection of elements within a page, producing undoable actions.</summary>
public static class AlignmentHelper
{
    /// <summary>Aligns or distributes the given elements according to the specified mode.
    /// Returns a CompositeAction that can be pushed to the undo manager.</summary>
    public static IUndoableAction Align(
        SlidePage page,
        IReadOnlyList<SlideElement> selection,
        AlignMode mode,
        double canvasWidth = 1920,
        double canvasHeight = 1080)
    {
        if (selection.Count < 2 && mode >= AlignMode.DistributeH)
            return new CompositeAction { Description = "Distribute (no-op)" };

        var composite = new CompositeAction { Description = $"Align {mode}" };

        switch (mode)
        {
            case AlignMode.Left:
            {
                double target = selection.Min(e => e.X);
                foreach (var elem in selection)
                    composite.Add(MoveTo(elem, target, elem.Y));
                break;
            }
            case AlignMode.CenterH:
            {
                double centerX = selection.Average(e => e.X + e.Width / 2);
                foreach (var elem in selection)
                    composite.Add(MoveTo(elem, centerX - elem.Width / 2, elem.Y));
                break;
            }
            case AlignMode.Right:
            {
                double target = selection.Max(e => e.X + e.Width);
                foreach (var elem in selection)
                    composite.Add(MoveTo(elem, target - elem.Width, elem.Y));
                break;
            }
            case AlignMode.Top:
            {
                double target = selection.Min(e => e.Y);
                foreach (var elem in selection)
                    composite.Add(MoveTo(elem, elem.X, target));
                break;
            }
            case AlignMode.CenterV:
            {
                double centerY = selection.Average(e => e.Y + e.Height / 2);
                foreach (var elem in selection)
                    composite.Add(MoveTo(elem, elem.X, centerY - elem.Height / 2));
                break;
            }
            case AlignMode.Bottom:
            {
                double target = selection.Max(e => e.Y + e.Height);
                foreach (var elem in selection)
                    composite.Add(MoveTo(elem, elem.X, target - elem.Height));
                break;
            }
            case AlignMode.DistributeH:
            {
                var sorted = selection.OrderBy(e => e.X).ToList();
                if (sorted.Count < 2) break;
                double totalWidth = sorted.Sum(e => e.Width);
                double totalSpace = (sorted[^1].X + sorted[^1].Width) - sorted[0].X - totalWidth;
                double step = totalSpace / (sorted.Count - 1);
                double currentX = sorted[0].X + sorted[0].Width + step;
                for (int i = 1; i < sorted.Count - 1; i++)
                {
                    composite.Add(MoveTo(sorted[i], currentX, sorted[i].Y));
                    currentX += sorted[i].Width + step;
                }
                break;
            }
            case AlignMode.DistributeV:
            {
                var sorted = selection.OrderBy(e => e.Y).ToList();
                if (sorted.Count < 2) break;
                double totalHeight = sorted.Sum(e => e.Height);
                double totalSpace = (sorted[^1].Y + sorted[^1].Height) - sorted[0].Y - totalHeight;
                double step = totalSpace / (sorted.Count - 1);
                double currentY = sorted[0].Y + sorted[0].Height + step;
                for (int i = 1; i < sorted.Count - 1; i++)
                {
                    composite.Add(MoveTo(sorted[i], sorted[i].X, currentY));
                    currentY += sorted[i].Height + step;
                }
                break;
            }
        }

        return composite;
    }

    /// <summary>Centers all selected elements on the canvas.</summary>
    public static IUndoableAction CenterOnCanvas(
        SlidePage page,
        IReadOnlyList<SlideElement> selection,
        double canvasWidth = 1920,
        double canvasHeight = 1080)
    {
        var composite = new CompositeAction { Description = "Center on canvas" };
        foreach (var elem in selection)
        {
            double cx = (canvasWidth - elem.Width) / 2;
            double cy = (canvasHeight - elem.Height) / 2;
            composite.Add(MoveTo(elem, cx, cy));
        }
        return composite;
    }

    /// <summary>Resizes all selected elements to the same width/height as the reference (largest).</summary>
    public static IUndoableAction MatchSize(
        SlidePage page,
        IReadOnlyList<SlideElement> selection,
        bool matchWidth = true,
        bool matchHeight = true)
    {
        if (selection.Count < 2)
            return new CompositeAction { Description = "Match size (no-op)" };

        var composite = new CompositeAction { Description = "Match size" };
        double targetW = selection.Max(e => e.Width);
        double targetH = selection.Max(e => e.Height);

        foreach (var elem in selection)
        {
            double w = matchWidth ? targetW : elem.Width;
            double h = matchHeight ? targetH : elem.Height;
            if (w != elem.Width || h != elem.Height)
                composite.Add(new ResizeElementAction(elem, elem.Width, elem.Height, w, h));
        }

        return composite;
    }

    private static MoveElementAction MoveTo(SlideElement elem, double newX, double newY)
    {
        return new MoveElementAction(elem, elem.X, elem.Y, newX, newY);
    }
}

/// <summary>Resizes an element, supporting undo/redo.</summary>
public sealed class ResizeElementAction : IUndoableAction
{
    private readonly SlideElement _element;
    private readonly double _oldW, _oldH, _newW, _newH;
    public string Description => "Resize element";

    public ResizeElementAction(SlideElement element, double oldW, double oldH, double newW, double newH)
    {
        _element = element;
        _oldW = oldW; _oldH = oldH;
        _newW = newW; _newH = newH;
    }

    public void Execute() => Apply(_newW, _newH);
    public void Undo() => Apply(_oldW, _oldH);
    public void Redo() => Apply(_newW, _newH);

    private void Apply(double w, double h) { _element.Width = w; _element.Height = h; }
}

/// <summary>Manages a selection set of element IDs.</summary>
public sealed class SelectionManager
{
    private readonly HashSet<string> _selected = new();
    private string? _anchorId;

    public IReadOnlyCollection<string> Selected => _selected;
    public int Count => _selected.Count;
    public bool IsEmpty => _selected.Count == 0;

    public event Action? SelectionChanged;

    public void Select(string elementId)
    {
        _selected.Clear();
        _selected.Add(elementId);
        _anchorId = elementId;
        SelectionChanged?.Invoke();
    }

    public void Toggle(string elementId)
    {
        if (_selected.Contains(elementId))
            _selected.Remove(elementId);
        else
            _selected.Add(elementId);
        SelectionChanged?.Invoke();
    }

    public void Add(string elementId)
    {
        _selected.Add(elementId);
        SelectionChanged?.Invoke();
    }

    public void SelectMultiple(IEnumerable<string> elementIds)
    {
        _selected.Clear();
        foreach (var id in elementIds) _selected.Add(id);
        SelectionChanged?.Invoke();
    }

    public void Clear()
    {
        if (_selected.Count == 0) return;
        _selected.Clear();
        _anchorId = null;
        SelectionChanged?.Invoke();
    }

    public bool Contains(string elementId) => _selected.Contains(elementId);

    /// <summary>Returns elements from the page that match the current selection.</summary>
    public List<SlideElement> GetSelectedElements(SlidePage page)
    {
        return page.Elements.Where(e => _selected.Contains(e.Id)).ToList();
    }

    /// <summary>Selects all elements on the page.</summary>
    public void SelectAll(SlidePage page)
    {
        _selected.Clear();
        foreach (var elem in page.Elements) _selected.Add(elem.Id);
        SelectionChanged?.Invoke();
    }
}
