namespace NanoUintEditor;

/// <summary>Represents the state of the editor's status bar.</summary>
public sealed class StatusBarModel
{
    private double _zoom = 1.0;
    private string _statusText = "Ready";
    private string _fileName = "";
    private bool _isDirty;
    private int _currentPage;
    private int _totalPages;
    private int _selectedCount;

    /// <summary>Current zoom level (0.1 to 5.0).</summary>
    public double Zoom
    {
        get => _zoom;
        set
        {
            var clamped = Math.Clamp(value, 0.1, 5.0);
            if (Math.Abs(_zoom - clamped) < 0.001) return;
            _zoom = clamped;
            ZoomChanged?.Invoke(this, clamped);
        }
    }

    /// <summary>Status text displayed in the left area.</summary>
    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; StatusTextChanged?.Invoke(this, value); }
    }

    /// <summary>Current file name (with dirty indicator).</summary>
    public string FileName
    {
        get => _fileName;
        set { _fileName = value; FileNameChanged?.Invoke(this, value); }
    }

    /// <summary>Whether the document has unsaved changes.</summary>
    public bool IsDirty
    {
        get => _isDirty;
        set { _isDirty = value; IsDirtyChanged?.Invoke(this, value); }
    }

    /// <summary>Current page index (1-based for display).</summary>
    public int CurrentPage
    {
        get => _currentPage;
        set { _currentPage = value; PageInfoChanged?.Invoke(this, EventArgs.Empty); }
    }

    /// <summary>Total number of pages.</summary>
    public int TotalPages
    {
        get => _totalPages;
        set { _totalPages = value; PageInfoChanged?.Invoke(this, EventArgs.Empty); }
    }

    /// <summary>Number of selected elements.</summary>
    public int SelectedCount
    {
        get => _selectedCount;
        set { _selectedCount = value; SelectedCountChanged?.Invoke(this, value); }
    }

    /// <summary>Formatted page info string: "Page 1 of 3".</summary>
    public string PageInfo => TotalPages > 0
        ? $"Page {_currentPage + 1} of {_totalPages}"
        : "No pages";

    /// <summary>Formatted zoom string: "100%".</summary>
    public string ZoomText => $"{(int)(_zoom * 100)}%";

    /// <summary>Whether the document is dirty (for title bar display).</summary>
    public string TitleSuffix => _isDirty ? " *" : "";

    // Events
    public event EventHandler<double>? ZoomChanged;
    public event EventHandler<string>? StatusTextChanged;
    public event EventHandler<string>? FileNameChanged;
    public event EventHandler<bool>? IsDirtyChanged;
    public event EventHandler? PageInfoChanged;
    public event EventHandler<int>? SelectedCountChanged;

    /// <summary>Sets zoom to a preset level.</summary>
    public void SetZoomPreset(string preset)
    {
        Zoom = preset switch
        {
            "25%" => 0.25,
            "50%" => 0.50,
            "75%" => 0.75,
            "100%" => 1.0,
            "125%" => 1.25,
            "150%" => 1.50,
            "200%" => 2.0,
            "Fit" => 1.0, // TODO: compute from canvas size
            _ => _zoom,
        };
    }

    /// <summary>Available zoom presets.</summary>
    public static string[] ZoomPresets { get; } =
        ["25%", "50%", "75%", "100%", "125%", "150%", "200%", "Fit"];
}
