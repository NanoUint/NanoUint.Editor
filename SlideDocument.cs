using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NanoUintEditor;

/// <summary>Format version for forward compatibility.</summary>
public static class DocumentFormat
{
    public const int CurrentVersion = 1;
}

/// <summary>A single element on a page (image, text, shape, etc.).</summary>
public class SlideElement
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "Element";
    public string Type { get; set; } = "Image"; // Image, Text, Shape, DialogueBox, ChoiceGroup, Audio, Flash
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 100;
    public double Height { get; set; } = 100;
    public double Rotation { get; set; }
    public double Opacity { get; set; } = 1.0;
    public int ZIndex { get; set; }
    public bool Visible { get; set; } = true;
    public string? ParentId { get; set; }

    /// <summary>Component-specific properties (sprite path, text content, font size, etc.).</summary>
    public Dictionary<string, object?> Props { get; set; } = new();
}

/// <summary>A single page (slide) in the document.</summary>
public class SlidePage
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "Page";
    public string? BackgroundPath { get; set; }

    /// <summary>Transition when entering this page (None, FlashBlack, Crossfade, etc.).</summary>
    public string TransitionType { get; set; } = "None";
    public float TransitionDuration { get; set; } = 0.6f;

    /// <summary>Elements on this page, ordered by ZIndex.</summary>
    public List<SlideElement> Elements { get; set; } = new();

    /// <summary>Speaker notes (not displayed during presentation).</summary>
    public string Notes { get; set; } = "";

    /// <summary>Thumbnail cache path (computed, not persisted).</summary>
    [JsonIgnore]
    public string? ThumbnailPath { get; set; }
}

/// <summary>Theme applied to the entire document (colors, fonts, master layouts).</summary>
public class SlideTheme
{
    public string Name { get; set; } = "Default";
    public string? BackgroundColor { get; set; }
    public string? FontFamily { get; set; }
    public double FontSize { get; set; } = 24;
    public string? TextColor { get; set; }
}

/// <summary>The root document model: a collection of pages with metadata.</summary>
public class SlideDocument
{
    public int FormatVersion { get; set; } = DocumentFormat.CurrentVersion;
    public string DocumentId { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "Untitled";
    public string? FilePath { get; set; }
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
    public SlideTheme Theme { get; set; } = new();
    public List<SlidePage> Pages { get; set; } = new();

    /// <summary>Active page index (not persisted).</summary>
    [JsonIgnore]
    public int CurrentPageIndex { get; set; }

    [JsonIgnore]
    public SlidePage? CurrentPage => Pages.Count > 0 ? Pages[CurrentPageIndex] : null;

    // Serialization helpers

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static SlideDocument? FromJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<SlideDocument>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public void Save(string path)
    {
        FilePath = path;
        LastModified = DateTime.UtcNow;
        File.WriteAllText(path, ToJson());
    }

    public static SlideDocument? Load(string path)
    {
        if (!File.Exists(path)) return null;
        var json = File.ReadAllText(path);
        var doc = FromJson(json);
        if (doc != null) doc.FilePath = path;
        return doc;
    }
}

/// <summary>Extension methods for converting between SlideDocument and the legacy SceneModel.</summary>
public static class SlideDocumentExtensions
{
    /// <summary>Converts a SlideDocument to legacy SceneObject list (for backward compatibility).</summary>
    public static List<SceneObject> ToSceneObjects(this SlideDocument doc, int pageIndex = 0)
    {
        if (pageIndex < 0 || pageIndex >= doc.Pages.Count) return new();
        var page = doc.Pages[pageIndex];
        var objects = new List<SceneObject>();

        foreach (var elem in page.Elements)
        {
            var obj = new SceneObject
            {
                Id = elem.Id,
                Name = elem.Name,
                ParentId = elem.ParentId,
                X = elem.X,
                Y = elem.Y,
                Width = elem.Width,
                Height = elem.Height,
                Opacity = elem.Opacity,
                SortingOrder = elem.ZIndex,
            };

            // Convert element type to component
            switch (elem.Type)
            {
                case "Image":
                    obj.Components.Add(new ComponentData
                    {
                        Type = "SpriteRenderer",
                        Props = new Dictionary<string, object?>
                        {
                            ["sprite"] = elem.Props.GetValueOrDefault("spritePath")
                        }
                    });
                    break;
                case "Text":
                    obj.Components.Add(new ComponentData
                    {
                        Type = "TextRenderer",
                        Props = new Dictionary<string, object?>
                        {
                            ["text"] = elem.Props.GetValueOrDefault("content"),
                            ["fontSize"] = elem.Props.GetValueOrDefault("fontSize") ?? 24.0,
                            ["color"] = elem.Props.GetValueOrDefault("textColor"),
                        }
                    });
                    break;
                case "DialogueBox":
                    obj.Components.Add(new ComponentData
                    {
                        Type = "DialogueBox",
                        Props = new Dictionary<string, object?>
                        {
                            ["speakerName"] = elem.Props.GetValueOrDefault("speaker"),
                            ["text"] = elem.Props.GetValueOrDefault("text"),
                        }
                    });
                    break;
                case "ChoiceGroup":
                    obj.Components.Add(new ComponentData
                    {
                        Type = "ChoiceGroup",
                        Props = new Dictionary<string, object?>
                        {
                            ["options"] = elem.Props.GetValueOrDefault("options"),
                        }
                    });
                    break;
                case "Audio":
                    obj.Components.Add(new ComponentData
                    {
                        Type = "AudioSource",
                        Props = new Dictionary<string, object?>
                        {
                            ["clip"] = elem.Props.GetValueOrDefault("clipPath"),
                            ["volume"] = elem.Props.GetValueOrDefault("volume") ?? 1.0,
                            ["loop"] = elem.Props.GetValueOrDefault("loop") ?? false,
                        }
                    });
                    break;
                case "Flash":
                    obj.Components.Add(new ComponentData
                    {
                        Type = "FlashOverlay",
                        Props = new Dictionary<string, object?>
                        {
                            ["color"] = elem.Props.GetValueOrDefault("color") ?? "#000000",
                            ["opacity"] = elem.Props.GetValueOrDefault("opacity") ?? 0.5,
                        }
                    });
                    break;
                case "AdvanceIndicator":
                    obj.Components.Add(new ComponentData { Type = "AdvanceIndicator" });
                    break;
            }

            // Add Transform component
            obj.Components.Insert(0, new ComponentData
            {
                Type = "Transform",
                Props = new Dictionary<string, object?>
                {
                    ["X"] = elem.X / 1920.0,
                    ["Y"] = elem.Y / 1080.0,
                    ["SortingOrder"] = (double)elem.ZIndex,
                    ["Opacity"] = elem.Opacity,
                }
            });

            objects.Add(obj);
        }

        return objects;
    }

    /// <summary>Creates a new page from legacy SceneObject list.</summary>
    public static SlidePage FromSceneObjects(string pageName, IEnumerable<SceneObject> objects)
    {
        var page = new SlidePage { Name = pageName };

        foreach (var obj in objects)
        {
            var elem = new SlideElement
            {
                Id = obj.Id,
                Name = obj.Name,
                X = obj.X,
                Y = obj.Y,
                Width = obj.Width,
                Height = obj.Height,
                Opacity = obj.Opacity,
                ZIndex = obj.SortingOrder,
                ParentId = obj.ParentId,
            };

            // Determine element type from components
            if (obj.HasComponent("SpriteRenderer"))
            {
                elem.Type = "Image";
                var sr = obj.GetComponent("SpriteRenderer");
                if (sr != null)
                {
                    elem.Props["spritePath"] = sr.Props.GetValueOrDefault("sprite");
                    elem.Props["color"] = sr.Props.GetValueOrDefault("color");
                }
            }
            else if (obj.HasComponent("TextRenderer"))
            {
                elem.Type = "Text";
                var tr = obj.GetComponent("TextRenderer");
                if (tr != null)
                {
                    elem.Props["content"] = tr.Props.GetValueOrDefault("text");
                    elem.Props["fontSize"] = tr.Props.GetValueOrDefault("fontSize");
                    elem.Props["textColor"] = tr.Props.GetValueOrDefault("color");
                }
            }
            else if (obj.HasComponent("DialogueBox"))
            {
                elem.Type = "DialogueBox";
                var db = obj.GetComponent("DialogueBox");
                if (db != null)
                {
                    elem.Props["speaker"] = db.Props.GetValueOrDefault("speakerName");
                    elem.Props["text"] = db.Props.GetValueOrDefault("text");
                }
            }
            else if (obj.HasComponent("ChoiceGroup"))
            {
                elem.Type = "ChoiceGroup";
            }
            else if (obj.HasComponent("AudioSource"))
            {
                elem.Type = "Audio";
            }
            else if (obj.HasComponent("FlashOverlay"))
            {
                elem.Type = "Flash";
            }
            else if (obj.HasComponent("AdvanceIndicator"))
            {
                elem.Type = "AdvanceIndicator";
            }
            else
            {
                elem.Type = "Shape";
            }

            page.Elements.Add(elem);
        }

        return page;
    }
}
