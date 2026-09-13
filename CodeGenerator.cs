using System.Text;

namespace NanoUintEditor;

/// <summary>Generates a C# VNScreen subclass from a slide document (targets NanoUintVN API).</summary>
public static class CodeGenerator
{
    /// <summary>Legacy overload: generates from SceneObject list (backward compatible).</summary>
    public static string Generate(IReadOnlyList<SceneObject> objects, string sceneName, Func<string, double, float> computePpu)
    {
        var page = SlideDocumentExtensions.FromSceneObjects(sceneName, objects);
        var doc = new SlideDocument { Title = sceneName };
        doc.Pages.Add(page);
        return Generate(doc, 0, computePpu);
    }

    /// <summary>Generates code from a SlideDocument page. computePpu: (spritePath, editorWidthPx) to engine pixelsPerUnit.</summary>
    public static string Generate(SlideDocument doc, int pageIndex, Func<string, double, float> computePpu)
    {
        var sb = new StringBuilder();
        var name = EditorUtils.SanitizeIdentifier(doc.Title);
        if (string.IsNullOrEmpty(name)) name = "Screen";

        sb.AppendLine("using System; using System.Collections; using NanoUint; using NanoUint.Drawing; using NanoUintVN; using NanoUintVN.Dialogue;");
        sb.AppendLine();
        sb.AppendLine($"public class {name} : VNScreen");
        sb.AppendLine("{");
        sb.AppendLine($"    public {name}(Scene scene) : base(scene) {{ }}");
        sb.AppendLine();
        sb.AppendLine("    protected override void OnBuild()");
        sb.AppendLine("    {");

        if (pageIndex < 0 || pageIndex >= doc.Pages.Count)
        {
            sb.AppendLine("        // No pages");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        var page = doc.Pages[pageIndex];
        EmitPage(sb, page, computePpu);

        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>Generates a full VNDocument from all pages.</summary>
    public static string GenerateVNDocument(SlideDocument doc, Func<string, double, float> computePpu)
    {
        var sb = new StringBuilder();
        var name = EditorUtils.SanitizeIdentifier(doc.Title);
        if (string.IsNullOrEmpty(name)) name = "GameContent";

        sb.AppendLine("using System; using System.Collections; using System.Collections.Generic; using NanoUint; using NanoUint.Drawing; using NanoUintVN; using NanoUintVN.Dialogue;");
        sb.AppendLine();
        sb.AppendLine($"public static class {name}");
        sb.AppendLine("{");
        sb.AppendLine($"    public static VNDocument CreateDocument()");
        sb.AppendLine("    {");
        sb.AppendLine($"        var pages = new List<VNPage>();");

        for (int i = 0; i < doc.Pages.Count; i++)
        {
            sb.AppendLine();
            sb.AppendLine($"        // Page {i}: {EditorUtils.Escape(doc.Pages[i].Name)}");
            EmitPageAsVNPage(sb, doc.Pages[i], i, computePpu);
        }

        sb.AppendLine();
        sb.AppendLine($"        return new VNDocument {{ DocumentId = \"{doc.DocumentId}\", Title = \"{EditorUtils.Escape(doc.Title)}\", Pages = pages }};");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void EmitPage(StringBuilder sb, SlidePage page, Func<string, double, float> computePpu)
    {
        foreach (var elem in page.Elements.OrderBy(e => e.ZIndex))
        {
            sb.AppendLine();
            sb.AppendLine($"        // {elem.Name} ({elem.Type})");
            var id = elem.Id;

            switch (elem.Type)
            {
                case "Image":
                    EmitImage(sb, elem, computePpu);
                    break;
                case "Text":
                    EmitText(sb, elem);
                    break;
                case "DialogueBox":
                    EmitDialogueBox(sb, elem);
                    break;
                case "ChoiceGroup":
                    EmitChoiceGroup(sb, elem);
                    break;
                case "Audio":
                    EmitAudio(sb, elem);
                    break;
                case "Flash":
                    EmitFlash(sb, elem);
                    break;
                case "AdvanceIndicator":
                    sb.AppendLine($"        var ai{id} = go{id}.AddComponent<AdvanceIndicator>();");
                    break;
            }

            // Transform
            sb.AppendLine($"        go{id}.Transform.X = {(elem.X / 1920.0).ToString("F4")}F;");
            sb.AppendLine($"        go{id}.Transform.Y = {(elem.Y / 1080.0).ToString("F4")}F;");
            sb.AppendLine($"        go{id}.Transform.SortingOrder = {elem.ZIndex};");
            if (Math.Abs(elem.Opacity - 1.0) > 0.001)
                sb.AppendLine($"        go{id}.Transform.Opacity = {elem.Opacity.ToString("F2")}F;");
        }

        // Parent-child links (after all objects exist)
        foreach (var elem in page.Elements.Where(e => e.ParentId != null))
        {
            var parent = page.Elements.FirstOrDefault(e => e.Id == elem.ParentId);
            if (parent != null)
                sb.AppendLine($"        go{elem.Id}.Transform.SetParent(go{parent.Id}.Transform);");
        }
    }

    private static void EmitPageAsVNPage(StringBuilder sb, SlidePage page, int index, Func<string, double, float> computePpu)
    {
        sb.AppendLine($"        pages.Add(new VNPage");
        sb.AppendLine("        {");
        sb.AppendLine($"            PageId = \"{page.Id}\",");
        sb.AppendLine($"            EntryTransition = PageTransition.{page.TransitionType}({page.TransitionDuration}f),");
        sb.AppendLine("            Elements = new List<VNElement>");
        sb.AppendLine("            {");

        foreach (var elem in page.Elements)
        {
            switch (elem.Type)
            {
                case "Image":
                    var spritePath = elem.Props.GetValueOrDefault("spritePath") as string ?? "";
                    sb.AppendLine($"                new BackgroundElement {{ SpritePath = \"{EditorUtils.Escape(spritePath)}\" }},");
                    break;
                case "Text":
                    var content = elem.Props.GetValueOrDefault("content") as string ?? "";
                    sb.AppendLine($"                // Text: {EditorUtils.Escape(content)}");
                    break;
                case "DialogueBox":
                    var speaker = elem.Props.GetValueOrDefault("speaker") as string ?? "";
                    var text = elem.Props.GetValueOrDefault("text") as string ?? "";
                    sb.AppendLine($"                // Dialogue: {EditorUtils.Escape(speaker)}: {EditorUtils.Escape(text)}");
                    break;
            }
        }

        sb.AppendLine("            },");
        sb.AppendLine("            Beats = new List<DialogueBeat>");
        sb.AppendLine("            {");

        // Emit dialogue beats from DialogueBox elements
        foreach (var elem in page.Elements.Where(e => e.Type == "DialogueBox"))
        {
            var speaker = elem.Props.GetValueOrDefault("speaker") as string ?? "";
            var text = elem.Props.GetValueOrDefault("text") as string ?? "";
            sb.AppendLine($"                new DialogueBeat {{ Speaker = \"{EditorUtils.Escape(speaker)}\", Text = \"{EditorUtils.Escape(text)}\" }},");
        }

        sb.AppendLine("            }");
        sb.AppendLine("        });");
    }

    private static void EmitImage(StringBuilder sb, SlideElement elem, Func<string, double, float> computePpu)
    {
        var id = elem.Id;
        var spritePath = elem.Props.GetValueOrDefault("spritePath") as string ?? "";
        sb.AppendLine($"        var go{id} = Scene.AddObject(\"{EditorUtils.Escape(elem.Name)}\");");
        sb.AppendLine($"        var sr{id} = go{id}.AddComponent<SpriteRenderer>();");
        if (!string.IsNullOrEmpty(spritePath))
        {
            var ppu = computePpu(spritePath, elem.Width);
            if (Math.Abs(ppu - 100f) < 0.5f)
                sb.AppendLine($"        sr{id}.Sprite = AssetDatabase.Load<Sprite>(\"{EditorUtils.Escape(spritePath)}\");");
            else
                sb.AppendLine($"        sr{id}.Sprite = AssetDatabase.Load<Sprite>(\"{EditorUtils.Escape(spritePath)}\", {ppu:F0}f);");
        }
        var tintColor = elem.Props.GetValueOrDefault("color") as string;
        if (!string.IsNullOrEmpty(tintColor))
            sb.AppendLine($"        sr{id}.Tint = {EditorUtils.ColorExpr(tintColor)};");
    }

    private static void EmitText(StringBuilder sb, SlideElement elem)
    {
        var id = elem.Id;
        var content = elem.Props.GetValueOrDefault("content") as string ?? "";
        var fontSize = elem.Props.GetValueOrDefault("fontSize") as double? ?? 24;
        var textColor = elem.Props.GetValueOrDefault("textColor") as string;
        sb.AppendLine($"        var go{id} = Scene.AddObject(\"{EditorUtils.Escape(elem.Name)}\");");
        sb.AppendLine($"        var tr{id} = go{id}.AddComponent<TextRenderer>();");
        sb.AppendLine($"        tr{id}.Content = \"{EditorUtils.Escape(content)}\";");
        sb.AppendLine($"        tr{id}.FontSize = {fontSize:F0}F;");
        if (!string.IsNullOrEmpty(textColor))
            sb.AppendLine($"        tr{id}.TextColor = {EditorUtils.ColorExpr(textColor)};");
    }

    private static void EmitDialogueBox(StringBuilder sb, SlideElement elem)
    {
        var id = elem.Id;
        var speaker = elem.Props.GetValueOrDefault("speaker") as string ?? "";
        var text = elem.Props.GetValueOrDefault("text") as string ?? "";
        sb.AppendLine($"        var go{id} = Scene.AddObject(\"{EditorUtils.Escape(elem.Name)}\");");
        sb.AppendLine($"        var db{id} = go{id}.AddComponent<DialogueBox>();");
        sb.AppendLine($"        db{id}.Show({(string.IsNullOrEmpty(speaker) ? "null" : "\"" + EditorUtils.Escape(speaker) + "\"")}, \"{EditorUtils.Escape(text)}\");");
    }

    private static void EmitChoiceGroup(StringBuilder sb, SlideElement elem)
    {
        var id = elem.Id;
        sb.AppendLine($"        var go{id} = Scene.AddObject(\"{EditorUtils.Escape(elem.Name)}\");");
        sb.AppendLine($"        var cg{id} = go{id}.AddComponent<ChoiceGroup>();");
        var options = elem.Props.GetValueOrDefault("options") as string;
        if (!string.IsNullOrWhiteSpace(options))
        {
            var arr = string.Join(", ", options.Split('|').Select(o => $"\"{EditorUtils.Escape(o.Trim())}\""));
            sb.AppendLine($"        cg{id}.Show(new[] {{ {arr} }});");
        }
    }

    private static void EmitAudio(StringBuilder sb, SlideElement elem)
    {
        var id = elem.Id;
        var clipPath = elem.Props.GetValueOrDefault("clipPath") as string ?? "";
        var volume = elem.Props.GetValueOrDefault("volume") as double? ?? 1.0;
        var loop = elem.Props.GetValueOrDefault("loop") as bool? ?? false;
        sb.AppendLine($"        var go{id} = Scene.AddObject(\"{EditorUtils.Escape(elem.Name)}\");");
        sb.AppendLine($"        var aud{id} = go{id}.AddComponent<AudioSource>();");
        if (!string.IsNullOrEmpty(clipPath))
            sb.AppendLine($"        aud{id}.Clip = AssetDatabase.Load<AudioClip>(\"{EditorUtils.Escape(clipPath)}\");");
        sb.AppendLine($"        aud{id}.Volume = {volume:F2}F;");
        sb.AppendLine($"        aud{id}.IsLooping = {loop.ToString().ToLower()};");
    }

    private static void EmitFlash(StringBuilder sb, SlideElement elem)
    {
        var id = elem.Id;
        var color = elem.Props.GetValueOrDefault("color") as string ?? "#000000";
        var opacity = elem.Props.GetValueOrDefault("opacity") as double? ?? 0.5;
        sb.AppendLine($"        var go{id} = Scene.AddObject(\"{EditorUtils.Escape(elem.Name)}\");");
        sb.AppendLine($"        var fo{id} = go{id}.AddComponent<FlashOverlay>();");
        sb.AppendLine($"        fo{id}.Color = {EditorUtils.ColorExpr(color, (float)opacity)};");
    }
}
