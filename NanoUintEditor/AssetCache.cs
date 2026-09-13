using System.Windows.Media.Imaging;

namespace NanoUintEditor;

/// <summary>Asset path resolution and decoded-image cache (avoids re-decoding large files on drag/resize).</summary>
public static class AssetCache
{
    public static readonly string ResourcesRoot = System.IO.Path.GetFullPath(
        System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "SteinsGateX", "Resources"));

    private static readonly Dictionary<string, BitmapImage> ImageCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Relative path to absolute disk path, with a filename fallback search.</summary>
    public static string FindAssetPath(string relPath)
    {
        var full = System.IO.Path.Combine(ResourcesRoot, relPath.Replace('/', '\\'));
        if (System.IO.File.Exists(full)) return full;
        if (System.IO.Directory.Exists(ResourcesRoot))
        {
            var results = System.IO.Directory.GetFiles(ResourcesRoot, System.IO.Path.GetFileName(relPath), System.IO.SearchOption.AllDirectories);
            if (results.Length > 0) return results[0];
        }
        return full;
    }

    /// <summary>Reads the native image size (cached).</summary>
    public static (double w, double h)? GetNativeImageSize(string relPath)
    {
        var bmp = LoadImage(relPath);
        if (bmp != null && bmp.PixelWidth > 0 && bmp.PixelHeight > 0)
            return (bmp.PixelWidth, bmp.PixelHeight);
        return null;
    }

    /// <summary>Loads an image by absolute or relative path; caches and decodes once via OnLoad.</summary>
    public static BitmapImage? LoadImage(string path)
    {
        var full = System.IO.File.Exists(path) ? path : FindAssetPath(path);
        if (!System.IO.File.Exists(full)) return null;
        lock (ImageCache)
        {
            if (ImageCache.TryGetValue(full, out var cached)) return cached;
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(full);
                bmp.EndInit();
                bmp.Freeze();
                ImageCache[full] = bmp;
                return bmp;
            }
            catch { return null; }
        }
    }
}
