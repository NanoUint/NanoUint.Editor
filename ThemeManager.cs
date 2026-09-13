namespace NanoUintEditor;

/// <summary>Theme modes supported by the editor.</summary>
public enum ThemeMode
{
    Light,
    Dark,
    System
}

/// <summary>Represents a complete color palette for the editor UI.</summary>
public sealed class ThemeColors
{
    // Base
    public string Background { get; init; } = "#FFFFFF";
    public string Foreground { get; init; } = "#1B1B1B";
    public string Surface { get; init; } = "#F5F5F5";
    public string SurfaceVariant { get; init; } = "#E8E8E8";

    // Borders
    public string Border { get; init; } = "#D1D1D1";
    public string BorderStrong { get; init; } = "#ADADAD";

    // Accent
    public string Accent { get; init; } = "#0078D4";
    public string AccentLight { get; init; } = "#C7E0F4";
    public string AccentDark { get; init; } = "#005A9E";

    // Status
    public string Success { get; init; } = "#107C10";
    public string Warning { get; init; } = "#FF8C00";
    public string Error { get; init; } = "#D13438";

    // Canvas
    public string CanvasBackground { get; init; } = "#E0E0E0";
    public string CanvasGrid { get; init; } = "#CCCCCC";

    // Text
    public string TextPrimary { get; init; } = "#1B1B1B";
    public string TextSecondary { get; init; } = "#616161";
    public string TextDisabled { get; init; } = "#BDBDBD";
    public string TextOnAccent { get; init; } = "#FFFFFF";

    // Panel
    public string PanelBackground { get; init; } = "#F5F5F5";
    public string PanelHeader { get; init; } = "#E0E0E0";
    public string PanelSelected { get; init; } = "#C7E0F4";

    /// <summary>Returns the Light theme palette.</summary>
    public static ThemeColors Light() => new();

    /// <summary>Returns the Dark theme palette.</summary>
    public static ThemeColors Dark() => new()
    {
        Background = "#1E1E1E",
        Foreground = "#F0F0F0",
        Surface = "#2D2D2D",
        SurfaceVariant = "#3E3E3E",
        Border = "#404040",
        BorderStrong = "#5A5A5A",
        Accent = "#4CC2FF",
        AccentLight = "#1A3A5C",
        AccentDark = "#6DD8FF",
        Success = "#6CCD5B",
        Warning = "#F7630C",
        Error = "#F2524D",
        CanvasBackground = "#2A2A2A",
        CanvasGrid = "#3A3A3A",
        TextPrimary = "#F0F0F0",
        TextSecondary = "#A0A0A0",
        TextDisabled = "#5A5A5A",
        TextOnAccent = "#000000",
        PanelBackground = "#252525",
        PanelHeader = "#333333",
        PanelSelected = "#1A3A5C",
    };
}

/// <summary>Manages editor theme switching with resource dictionary updates.</summary>
public sealed class ThemeManager
{
    private ThemeMode _mode = ThemeMode.Light;
    private ThemeColors _current = ThemeColors.Light();

    public ThemeMode Mode
    {
        get => _mode;
        set
        {
            if (_mode == value) return;
            var old = _mode;
            _mode = value;
            _current = value switch
            {
                ThemeMode.Dark => ThemeColors.Dark(),
                _ => ThemeColors.Light(),
            };
            ModeChanged?.Invoke(this, new ThemeChangedEventArgs(old, value, _current));
        }
    }

    public ThemeColors Current => _current;

    /// <summary>Fired when theme mode changes.</summary>
    public event EventHandler<ThemeChangedEventArgs>? ModeChanged;

    /// <summary>Gets a color by name from the current theme.</summary>
    public string GetColor(string name) => name switch
    {
        nameof(ThemeColors.Background) => _current.Background,
        nameof(ThemeColors.Foreground) => _current.Foreground,
        nameof(ThemeColors.Surface) => _current.Surface,
        nameof(ThemeColors.SurfaceVariant) => _current.SurfaceVariant,
        nameof(ThemeColors.Border) => _current.Border,
        nameof(ThemeColors.BorderStrong) => _current.BorderStrong,
        nameof(ThemeColors.Accent) => _current.Accent,
        nameof(ThemeColors.AccentLight) => _current.AccentLight,
        nameof(ThemeColors.AccentDark) => _current.AccentDark,
        nameof(ThemeColors.Success) => _current.Success,
        nameof(ThemeColors.Warning) => _current.Warning,
        nameof(ThemeColors.Error) => _current.Error,
        nameof(ThemeColors.CanvasBackground) => _current.CanvasBackground,
        nameof(ThemeColors.CanvasGrid) => _current.CanvasGrid,
        nameof(ThemeColors.TextPrimary) => _current.TextPrimary,
        nameof(ThemeColors.TextSecondary) => _current.TextSecondary,
        nameof(ThemeColors.TextDisabled) => _current.TextDisabled,
        nameof(ThemeColors.TextOnAccent) => _current.TextOnAccent,
        nameof(ThemeColors.PanelBackground) => _current.PanelBackground,
        nameof(ThemeColors.PanelHeader) => _current.PanelHeader,
        nameof(ThemeColors.PanelSelected) => _current.PanelSelected,
        _ => _current.Background,
    };

    /// <summary>Returns all colors as a dictionary for resource dictionary binding.</summary>
    public Dictionary<string, string> GetAllColors() => new()
    {
        ["Background"] = _current.Background,
        ["Foreground"] = _current.Foreground,
        ["Surface"] = _current.Surface,
        ["SurfaceVariant"] = _current.SurfaceVariant,
        ["Border"] = _current.Border,
        ["BorderStrong"] = _current.BorderStrong,
        ["Accent"] = _current.Accent,
        ["AccentLight"] = _current.AccentLight,
        ["AccentDark"] = _current.AccentDark,
        ["Success"] = _current.Success,
        ["Warning"] = _current.Warning,
        ["Error"] = _current.Error,
        ["CanvasBackground"] = _current.CanvasBackground,
        ["CanvasGrid"] = _current.CanvasGrid,
        ["TextPrimary"] = _current.TextPrimary,
        ["TextSecondary"] = _current.TextSecondary,
        ["TextDisabled"] = _current.TextDisabled,
        ["TextOnAccent"] = _current.TextOnAccent,
        ["PanelBackground"] = _current.PanelBackground,
        ["PanelHeader"] = _current.PanelHeader,
        ["PanelSelected"] = _current.PanelSelected,
    };
}

/// <summary>Event args for theme changes.</summary>
public sealed class ThemeChangedEventArgs : EventArgs
{
    public ThemeMode OldMode { get; init; }
    public ThemeMode NewMode { get; init; }
    public ThemeColors Colors { get; init; } = null!;

    public ThemeChangedEventArgs(ThemeMode oldMode, ThemeMode newMode, ThemeColors colors)
    {
        OldMode = oldMode;
        NewMode = newMode;
        Colors = colors;
    }
}
