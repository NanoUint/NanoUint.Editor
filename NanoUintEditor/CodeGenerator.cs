using System.Text;

namespace NanoUintEditor;

/// <summary>Generates a C# ScreenBase subclass from a scene (aligned with the NanoUint/SteinsGateX API).</summary>
public static class CodeGenerator
{
    /// <summary>Generates code. computePpu: (spritePath, editorWidthPx) to engine pixelsPerUnit.</summary>
    public static string Generate(IReadOnlyList<SceneObject> objects, string sceneName, Func<string, double, float> computePpu)
    {
        var sb = new StringBuilder();
        var name = EditorUtils.SanitizeIdentifier(sceneName);
        if (string.IsNullOrEmpty(name)) name = "Screen";

        sb.AppendLine("using System; using System.Collections; using NanoUint; using NanoUint.Drawing; using SteinsGateX; using SteinsGateX.Screen;");
        sb.AppendLine();
        sb.AppendLine($"public class {name} : ScreenBase");
        sb.AppendLine("{");
        sb.AppendLine($"    public {name}(Scene scene) : base(scene) {{ }}");
        sb.AppendLine();
        sb.AppendLine("    protected override void OnBuild()");
        sb.AppendLine("    {");

        foreach (var obj in objects.OrderBy(o => o.SortingOrder))
        {
            var id = obj.Id;
            sb.AppendLine();
            sb.AppendLine($"        // {obj.Name}");
            sb.AppendLine($"        var go{id} = new GameObject(\"{EditorUtils.Escape(obj.Name)}\");");
            sb.AppendLine($"        Scene.AddObject(go{id});");

            // SpriteRenderer must be emitted before SpriteButton (SpriteButton.Awake caches the renderer reference)
            var srComp = obj.GetComponent("SpriteRenderer");
            if (srComp != null)
                EmitSpriteRenderer(sb, id, srComp.Props, obj, computePpu);

            foreach (var comp in obj.Components)
            {
                if (comp.Type == "SpriteRenderer" || comp.Type == "Transform") continue;
                var props = comp.Props;
                switch (comp.Type)
                {
                    case "TextRenderer":
                        sb.AppendLine($"        var tr{id} = go{id}.AddComponent<TextRenderer>();");
                        if (props.TryGetValue("text", out var tt) && tt is string tStr)
                            sb.AppendLine($"        tr{id}.Content = \"{EditorUtils.Escape(tStr)}\";");
                        if (props.TryGetValue("fontSize", out var fs))
                            sb.AppendLine($"        tr{id}.FontSize = {String.Format("{0:F0}", (fs as double?) ?? 24)}F;");
                        if (props.TryGetValue("color", out var tc) && tc is string tCol && !string.IsNullOrEmpty(tCol))
                            sb.AppendLine($"        tr{id}.TextColor = {EditorUtils.ColorExpr(tCol)};");
                        break;
                    case "SpriteButton":
                        sb.AppendLine($"        var sb{id} = go{id}.AddComponent<SpriteButton>();");
                        if (props.TryGetValue("sprite", out var bsp) && bsp is string bs && !string.IsNullOrEmpty(bs))
                            sb.AppendLine($"        sb{id}.Sprite = AssetDatabase.Load<Sprite>(\"{bs}\");");
                        if (props.TryGetValue("hoverSprite", out var hs) && hs is string hStr && !string.IsNullOrEmpty(hStr))
                            sb.AppendLine($"        sb{id}.HoverSprite = AssetDatabase.Load<Sprite>(\"{hStr}\");");
                        if (props.TryGetValue("onClick", out var oc) && oc is string oStr && !string.IsNullOrWhiteSpace(oStr))
                            sb.AppendLine($"        sb{id}.OnClick += () => {{ {oStr} }};");
                        break;
                    case "PassiveButton":
                        sb.AppendLine($"        var pb{id} = go{id}.AddComponent<PassiveButton>();");
                        if (props.TryGetValue("onClick", out var poc) && poc is string poStr && !string.IsNullOrWhiteSpace(poStr))
                            sb.AppendLine($"        pb{id}.OnClick += () => {{ {poStr} }};");
                        break;
                    case "BackgroundRenderer":
                        sb.AppendLine($"        var bg{id} = go{id}.AddComponent<BackgroundRenderer>();");
                        if (props.TryGetValue("sprite", out var bgsp) && bgsp is string bgPath && !string.IsNullOrEmpty(bgPath))
                            sb.AppendLine($"        bg{id}.Sprite = AssetDatabase.Load<Sprite>(\"{bgPath}\");");
                        break;
                    case "DialogueBox":
                        sb.AppendLine($"        var db{id} = go{id}.AddComponent<DialogueBox>();");
                        var sn = props.GetValueOrDefault("speakerName") as string ?? "";
                        var dt = props.GetValueOrDefault("text") as string ?? "";
                        sb.AppendLine($"        db{id}.Show({(string.IsNullOrEmpty(sn) ? "null" : "\"" + EditorUtils.Escape(sn) + "\"")}, \"{EditorUtils.Escape(dt)}\");");
                        break;
                    case "ChoiceGroup":
                        sb.AppendLine($"        var cg{id} = go{id}.AddComponent<ChoiceGroup>();");
                        if (props.TryGetValue("options", out var opts) && opts is string optsStr && !string.IsNullOrWhiteSpace(optsStr))
                        {
                            var arr = string.Join(", ", optsStr.Split('|').Select(o => $"\"{EditorUtils.Escape(o.Trim())}\""));
                            sb.AppendLine($"        cg{id}.Show(new[] {{ {arr} }});");
                        }
                        break;
                    case "AudioSource":
                        sb.AppendLine($"        var aud{id} = go{id}.AddComponent<AudioSource>();");
                        if (props.TryGetValue("clip", out var cl) && cl is string clStr && !string.IsNullOrEmpty(clStr))
                            sb.AppendLine($"        aud{id}.Clip = AssetDatabase.Load<AudioClip>(\"{clStr}\");");
                        if (props.TryGetValue("volume", out var vol))
                            sb.AppendLine($"        aud{id}.Volume = {String.Format("{0:F2}", (vol as double?) ?? 1.0)}F;");
                        if (props.TryGetValue("loop", out var lp))
                            sb.AppendLine($"        aud{id}.IsLooping = {((lp is true).ToString().ToLower())};");
                        break;
                    case "FlashOverlay":
                        sb.AppendLine($"        var fo{id} = go{id}.AddComponent<FlashOverlay>();");
                        var foCol = props.GetValueOrDefault("color") as string ?? "#000000";
                        var foAlpha = (props.GetValueOrDefault("opacity") as double?) ?? 0.5;
                        sb.AppendLine($"        fo{id}.Color = {EditorUtils.ColorExpr(foCol, (float)foAlpha)};");
                        break;
                    case "AdvanceIndicator":
                        sb.AppendLine($"        var ai{id} = go{id}.AddComponent<AdvanceIndicator>();");
                        break;
                    case "LineRenderer":
                        sb.AppendLine($"        var lr{id} = go{id}.AddComponent<LineRenderer>();");
                        var lrCol = props.GetValueOrDefault("color") as string ?? "#FFFFFF";
                        sb.AppendLine($"        lr{id}.Color = {EditorUtils.ColorExpr(lrCol)};");
                        var lrW = (props.GetValueOrDefault("thickness") as double?) ?? 2.0;
                        sb.AppendLine($"        lr{id}.Width = {String.Format("{0:F1}", lrW)}F;");
                        var lx1 = (props.GetValueOrDefault("x1") as double?) ?? 0; var ly1 = (props.GetValueOrDefault("y1") as double?) ?? 0;
                        var lx2 = (props.GetValueOrDefault("x2") as double?) ?? 100; var ly2 = (props.GetValueOrDefault("y2") as double?) ?? 0;
                        sb.AppendLine($"        lr{id}.SetPositions(new[] {{ new Vector2({(lx1 / 1920.0).ToString("F4")}f, {(ly1 / 1080.0).ToString("F4")}f), new Vector2({(lx2 / 1920.0).ToString("F4")}f, {(ly2 / 1080.0).ToString("F4")}f) }});");
                        break;
                    case "HintRenderer":
                        sb.AppendLine($"        // HintRenderer：引擎通过 PassiveButton.HintText 提供提示文本，无独立属性可生成");
                        break;
                    case "SDFTextRenderer":
                        sb.AppendLine($"        // SDFTextRenderer：引擎暂无对应组件，未生成代码");
                        break;
                }
            }

            // Transform (normalized coordinates relative to the parent)
            sb.AppendLine($"        go{id}.Transform.X = {(obj.X / 1920.0).ToString("F4")}F;");
            sb.AppendLine($"        go{id}.Transform.Y = {(obj.Y / 1080.0).ToString("F4")}F;");
            sb.AppendLine($"        go{id}.Transform.SortingOrder = {obj.SortingOrder};");
            if (Math.Abs(obj.Opacity - 1.0) > 0.001)
                sb.AppendLine($"        go{id}.Transform.Opacity = {obj.Opacity.ToString("F2")}F;");
        }

        // Parent-child links: set after all objects exist, so the parent is always created first
        foreach (var obj in objects.Where(o => o.ParentId != null))
        {
            var parent = objects.FirstOrDefault(o => o.Id == obj.ParentId);
            if (parent != null)
                sb.AppendLine($"        go{obj.Id}.Transform.SetParent(go{parent.Id}.Transform);");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void EmitSpriteRenderer(StringBuilder sb, string id, Dictionary<string, object?> props, SceneObject obj, Func<string, double, float> computePpu)
    {
        sb.AppendLine($"        var sr{id} = go{id}.AddComponent<SpriteRenderer>();");
        if (props.TryGetValue("sprite", out var sp) && sp is string sPath && !string.IsNullOrEmpty(sPath))
        {
            var ppu = computePpu(sPath, obj.Width);
            if (Math.Abs(ppu - 100f) < 0.5f)
                sb.AppendLine($"        sr{id}.Sprite = AssetDatabase.Load<Sprite>(\"{sPath}\");");
            else
                sb.AppendLine($"        sr{id}.Sprite = AssetDatabase.Load<Sprite>(\"{sPath}\", {ppu:F0}f);");
        }
        if (props.TryGetValue("color", out var sc) && sc is string sCol && !string.IsNullOrEmpty(sCol) && !sCol.Equals("#FFFFFF", StringComparison.OrdinalIgnoreCase))
            sb.AppendLine($"        sr{id}.Tint = {EditorUtils.ColorExpr(sCol)};");
    }
}
