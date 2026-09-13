namespace NanoUintEditor;

/// <summary>Known panel types in the editor.</summary>
public enum PanelType
{
    Properties,
    Hierarchy,
    Assets,
    Backlog,
    Output
}

/// <summary>Docking position for a panel.</summary>
public enum DockPosition
{
    Left,
    Right,
    Bottom,
    Floating
}

/// <summary>State of a single panel.</summary>
public sealed class PanelState
{
    public PanelType Type { get; init; }
    public string Title { get; set; }
    public bool IsVisible { get; set; } = true;
    public DockPosition Dock { get; set; }
    public double Width { get; set; } = 300;
    public double Height { get; set; } = 200;
    public double FloatingX { get; set; }
    public double FloatingY { get; set; }

    public PanelState(PanelType type, string title, DockPosition dock = DockPosition.Left)
    {
        Type = type;
        Title = title;
        Dock = dock;
    }
}

/// <summary>Manages visibility and docking of editor panels.</summary>
public sealed class PanelManager
{
    private readonly Dictionary<PanelType, PanelState> _panels = new();

    public IReadOnlyDictionary<PanelType, PanelState> Panels => _panels;

    /// <summary>Fired when a panel's visibility changes.</summary>
    public event Action<PanelType, bool>? VisibilityChanged;

    /// <summary>Fired when a panel's dock position changes.</summary>
    public event Action<PanelType, DockPosition>? DockChanged;

    public PanelManager()
    {
        _panels[PanelType.Properties] = new PanelState(PanelType.Properties, "Properties", DockPosition.Right);
        _panels[PanelType.Hierarchy] = new PanelState(PanelType.Hierarchy, "Hierarchy", DockPosition.Left);
        _panels[PanelType.Assets] = new PanelState(PanelType.Assets, "Assets", DockPosition.Bottom);
        _panels[PanelType.Backlog] = new PanelState(PanelType.Backlog, "Backlog", DockPosition.Bottom);
        _panels[PanelType.Output] = new PanelState(PanelType.Output, "Output", DockPosition.Bottom);
    }

    /// <summary>Toggles panel visibility.</summary>
    public void Toggle(PanelType panel)
    {
        if (!_panels.ContainsKey(panel)) return;
        _panels[panel].IsVisible = !_panels[panel].IsVisible;
        VisibilityChanged?.Invoke(panel, _panels[panel].IsVisible);
    }

    /// <summary>Shows a panel.</summary>
    public void Show(PanelType panel)
    {
        if (!_panels.ContainsKey(panel)) return;
        if (_panels[panel].IsVisible) return;
        _panels[panel].IsVisible = true;
        VisibilityChanged?.Invoke(panel, true);
    }

    /// <summary>Hides a panel.</summary>
    public void Hide(PanelType panel)
    {
        if (!_panels.ContainsKey(panel)) return;
        if (!_panels[panel].IsVisible) return;
        _panels[panel].IsVisible = false;
        VisibilityChanged?.Invoke(panel, false);
    }

    /// <summary>Hides all panels.</summary>
    public void HideAll()
    {
        foreach (var kv in _panels)
        {
            if (kv.Value.IsVisible)
            {
                kv.Value.IsVisible = false;
                VisibilityChanged?.Invoke(kv.Key, false);
            }
        }
    }

    /// <summary>Shows all panels.</summary>
    public void ShowAll()
    {
        foreach (var kv in _panels)
        {
            if (!kv.Value.IsVisible)
            {
                kv.Value.IsVisible = true;
                VisibilityChanged?.Invoke(kv.Key, true);
            }
        }
    }

    /// <summary>Sets the dock position of a panel.</summary>
    public void SetDock(PanelType panel, DockPosition position)
    {
        if (!_panels.ContainsKey(panel)) return;
        _panels[panel].Dock = position;
        DockChanged?.Invoke(panel, position);
    }

    /// <summary>Returns all visible panels.</summary>
    public IEnumerable<PanelState> GetVisiblePanels()
    {
        return _panels.Values.Where(p => p.IsVisible);
    }

    /// <summary>Returns panels at a specific dock position.</summary>
    public IEnumerable<PanelState> GetPanelsAt(DockPosition position)
    {
        return _panels.Values.Where(p => p.Dock == position && p.IsVisible);
    }

    /// <summary>Serializes panel state to a dictionary (for save/load).</summary>
    public Dictionary<string, object?> Serialize()
    {
        var result = new Dictionary<string, object?>();
        foreach (var kv in _panels)
        {
            result[$"{kv.Key}.Visible"] = kv.Value.IsVisible;
            result[$"{kv.Key}.Dock"] = kv.Value.Dock.ToString();
            result[$"{kv.Key}.Width"] = kv.Value.Width;
            result[$"{kv.Key}.Height"] = kv.Value.Height;
        }
        return result;
    }

    /// <summary>Restores panel state from a dictionary.</summary>
    public void Deserialize(Dictionary<string, object?> data)
    {
        foreach (var panel in _panels.Keys)
        {
            var prefix = $"{panel}.";
            if (data.ContainsKey($"{prefix}Visible") && data[$"{prefix}Visible"] is bool visible)
                _panels[panel].IsVisible = visible;
            if (data.ContainsKey($"{prefix}Dock") && data[$"{prefix}Dock"]?.ToString() is string dockStr
                && Enum.TryParse<DockPosition>(dockStr, out var dock))
                _panels[panel].Dock = dock;
            if (data.ContainsKey($"{prefix}Width") && data[$"{prefix}Width"] is double width)
                _panels[panel].Width = width;
            if (data.ContainsKey($"{prefix}Height") && data[$"{prefix}Height"] is double height)
                _panels[panel].Height = height;
        }
    }
}
