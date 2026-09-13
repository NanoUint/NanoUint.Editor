namespace NanoUintEditor;

/// <summary>Component definition (name/icon/color/addable/default properties).</summary>
public record ComponentDef(string Name, string Icon, string Color, bool Addable, Dictionary<string, object?> Defaults);

/// <summary>Registry of component types supported by the editor, with their appearance and defaults.</summary>
public static class ComponentRegistry
{
    public static readonly Dictionary<string, ComponentDef> All = new()
    {
        ["Transform"] = new("Transform", "📍", "#4A8CFF", false, new() { ["X"] = 0.0, ["Y"] = 0.0, ["Width"] = 100.0, ["Height"] = 100.0, ["Opacity"] = 1.0, ["SortingOrder"] = 0 }),
        ["SpriteRenderer"] = new("SpriteRenderer", "🖼", "#4EC97A", true, new() { ["sprite"] = "", ["color"] = "#FFFFFF", ["pixelsPerUnit"] = 100.0 }),
        ["TextRenderer"] = new("TextRenderer", "📝", "#E5904B", true, new() { ["text"] = "Hello", ["fontSize"] = 24.0, ["color"] = "#FFFFFF", ["font"] = "HarmonySans", ["alignment"] = "Left" }),
        ["SpriteButton"] = new("SpriteButton", "🔘", "#E5C04B", true, new() { ["sprite"] = "", ["hoverSprite"] = "", ["pressSprite"] = "", ["onClick"] = "" }),
        ["PassiveButton"] = new("PassiveButton", "⬜", "#B066FF", true, new() { ["onClick"] = "" }),
        ["BackgroundRenderer"] = new("BackgroundRenderer", "🏞", "#3EBFBF", true, new() { ["sprite"] = "", ["crossfade"] = true }),
        ["DialogueBox"] = new("DialogueBox", "💬", "#E0559A", true, new() { ["speakerName"] = "", ["text"] = "" }),
        ["ChoiceGroup"] = new("ChoiceGroup", "📋", "#E5C04B", true, new() { ["options"] = "Option A|Option B|Option C" }),
        ["AudioSource"] = new("AudioSource", "🔊", "#3EBFBF", true, new() { ["clip"] = "", ["volume"] = 1.0, ["loop"] = true }),
        ["FlashOverlay"] = new("FlashOverlay", "⬛", "#6A6E76", true, new() { ["color"] = "#000000", ["opacity"] = 0.5 }),
        ["AdvanceIndicator"] = new("AdvanceIndicator", "⏩", "#E0556A", true, new() { ["sprite"] = "" }),
        ["LineRenderer"] = new("LineRenderer", "📏", "#4A8CFF", true, new() { ["x1"] = 0.0, ["y1"] = 0.0, ["x2"] = 100.0, ["y2"] = 0.0, ["color"] = "#FFFFFF", ["thickness"] = 2.0 }),
        ["HintRenderer"] = new("HintRenderer", "💡", "#969AA3", true, new() { ["text"] = "Hint", ["sprite"] = "" }),
        ["SDFTextRenderer"] = new("SDFTextRenderer", "🔤", "#E5904B", true, new() { ["text"] = "SDF Text", ["fontSize"] = 24.0, ["color"] = "#FFFFFF" }),
    };

    /// <summary>Component names that can be added manually via the Inspector.</summary>
    public static readonly string[] Addable = All.Where(kv => kv.Value.Addable).Select(kv => kv.Key).ToArray();
}
