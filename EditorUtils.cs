using System.Windows.Media;

namespace NanoUintEditor;

/// <summary>Editor utilities (pure functions, testable).</summary>
public static class EditorUtils
{
    /// <summary>Scene name to a valid C# identifier.</summary>
    public static string SanitizeIdentifier(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new System.Text.StringBuilder();
        foreach (var c in s)
            sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
        if (char.IsDigit(sb[0])) sb.Insert(0, '_');
        return sb.ToString();
    }

    /// <summary>Escapes a C# string literal (backslashes and quotes).</summary>
    public static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    /// <summary>Hex color (#RRGGBB or the editor-saved #RRGGBBAA) to an engine Color expression.</summary>
    public static string ColorExpr(string hex, float alpha = 1f)
    {
        var s = (hex ?? "").Trim().TrimStart('#');
        try
        {
            if (s.Length == 6)
            {
                byte r = Convert.ToByte(s.Substring(0, 2), 16), g = Convert.ToByte(s.Substring(2, 2), 16), b = Convert.ToByte(s.Substring(4, 2), 16);
                if (alpha < 0.999f)
                {
                    byte a = (byte)(255 * Math.Clamp(alpha, 0f, 1f));
                    return $"NanoUint.Drawing.Color.FromRgba(0x{r:X2}, 0x{g:X2}, 0x{b:X2}, 0x{a:X2})";
                }
                return $"NanoUint.Drawing.Color.FromRgb(0x{r:X2}, 0x{g:X2}, 0x{b:X2})";
            }
            if (s.Length == 8) // RRGGBBAA (format saved by the editor)
            {
                byte r = Convert.ToByte(s.Substring(0, 2), 16), g = Convert.ToByte(s.Substring(2, 2), 16), b = Convert.ToByte(s.Substring(4, 2), 16), a = Convert.ToByte(s.Substring(6, 2), 16);
                if (alpha < 0.999f) a = (byte)(a * Math.Clamp(alpha, 0f, 1f));
                return a == 255
                    ? $"NanoUint.Drawing.Color.FromRgb(0x{r:X2}, 0x{g:X2}, 0x{b:X2})"
                    : $"NanoUint.Drawing.Color.FromRgba(0x{r:X2}, 0x{g:X2}, 0x{b:X2}, 0x{a:X2})";
            }
        }
        catch { }
        return "NanoUint.Drawing.Color.White";
    }

    /// <summary>Parses #RRGGBB[AA] or #AARRGGBB; returns (r,g,b,a), or white on failure.</summary>
    public static (byte r, byte g, byte b, byte a) ParseHex(string hex, byte alpha = 255)
    {
        var s = (hex ?? "").Trim().TrimStart('#');
        try
        {
            if (s.Length == 6)
                return (Convert.ToByte(s.Substring(0, 2), 16), Convert.ToByte(s.Substring(2, 2), 16), Convert.ToByte(s.Substring(4, 2), 16), alpha);
            if (s.Length == 8)
                return (Convert.ToByte(s.Substring(0, 2), 16), Convert.ToByte(s.Substring(2, 2), 16), Convert.ToByte(s.Substring(4, 2), 16), Convert.ToByte(s.Substring(6, 2), 16));
        }
        catch { }
        return (255, 255, 255, alpha);
    }

    public static SolidColorBrush ParseColorBrush(string hex)
    {
        try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
        catch { return Brushes.White; }
    }

    public static Color ParseMediaColor(string hex)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch { return Colors.White; }
    }

    public static System.Drawing.Color ParseDrawingColor(string hex)
    {
        try
        {
            var c = (Color)ColorConverter.ConvertFromString(hex);
            return System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);
        }
        catch { return System.Drawing.Color.White; }
    }
}
