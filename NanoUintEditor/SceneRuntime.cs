using NanoUint;
using NanoUint.Drawing;
using NanoUint.Rendering;

namespace NanoUintEditor;

/// <summary>Converts an editor scene into a NanoUint runtime scene for play-mode preview; matches CodeGenerator's property-to-engine-API mapping.</summary>
public static class SceneRuntime
{
    private const double RefWidth = 1920.0;
    private const double RefHeight = 1080.0;

    /// <summary>Builds the editor object tree into the preview host scene.</summary>
    public static void Build(ScenePreviewHost host, IReadOnlyList<SceneObject> objects)
    {
        host.Clear();
        var scene = host.Scene;
        var map = new Dictionary<string, GameObject>();

        // Engine asset directory (needed once per editor process)
        if (System.IO.Directory.Exists(AssetCache.ResourcesRoot))
            AssetDatabase.AddSearchDirectory(AssetCache.ResourcesRoot);

        foreach (var obj in objects.OrderBy(o => o.SortingOrder))
        {
            var go = new GameObject(obj.Name);
            scene.AddObject(go);
            map[obj.Id] = go;

            // SpriteRenderer must be added before SpriteButton (SpriteButton.Awake caches the renderer reference)
            var srComp = obj.GetComponent("SpriteRenderer");
            if (srComp != null) EmitSpriteRenderer(go, srComp.Props, obj);

            foreach (var comp in obj.Components)
            {
                if (comp.Type == "SpriteRenderer" || comp.Type == "Transform") continue;
                var props = comp.Props;
                switch (comp.Type)
                {
                    case "TextRenderer":
                        var tr = go.AddComponent<TextRenderer>();
                        if (props.TryGetValue("text", out var tt) && tt is string tStr) tr.Content = tStr;
                        if (props.TryGetValue("fontSize", out var fs)) tr.FontSize = (float)((fs as double?) ?? 24);
                        if (props.TryGetValue("color", out var tc) && tc is string tCol) tr.TextColor = ParseColor(tCol);
                        break;
                    case "SpriteButton":
                        var sb = go.AddComponent<SpriteButton>();
                        if (props.TryGetValue("sprite", out var bsp) && bsp is string bs && !string.IsNullOrEmpty(bs)) sb.Sprite = AssetDatabase.Load<Sprite>(bs);
                        if (props.TryGetValue("hoverSprite", out var hs) && hs is string hStr && !string.IsNullOrEmpty(hStr)) sb.HoverSprite = AssetDatabase.Load<Sprite>(hStr);
                        if (props.TryGetValue("onClick", out var oc) && oc is string oStr && !string.IsNullOrWhiteSpace(oStr))
                            sb.OnClick += () => NanoUint.Diagnostics.Logger.Info("Preview", $"Button '{go.Name}' clicked: {oStr}");
                        break;
                    case "PassiveButton":
                        var pb = go.AddComponent<PassiveButton>();
                        if (props.TryGetValue("onClick", out var poc) && poc is string poStr && !string.IsNullOrWhiteSpace(poStr))
                            pb.OnClick += () => NanoUint.Diagnostics.Logger.Info("Preview", $"PassiveButton '{go.Name}' clicked: {poStr}");
                        break;
                    case "BackgroundRenderer":
                        var bg = go.AddComponent<BackgroundRenderer>();
                        if (props.TryGetValue("sprite", out var bgsp) && bgsp is string bgPath && !string.IsNullOrEmpty(bgPath)) bg.Sprite = AssetDatabase.Load<Sprite>(bgPath);
                        break;
                    case "DialogueBox":
                        var db = go.AddComponent<DialogueBox>();
                        var sn = props.GetValueOrDefault("speakerName") as string ?? "";
                        var dt = props.GetValueOrDefault("text") as string ?? "";
                        db.Show(string.IsNullOrEmpty(sn) ? null : sn, dt);
                        break;
                    case "ChoiceGroup":
                        var cg = go.AddComponent<ChoiceGroup>();
                        if (props.TryGetValue("options", out var opts) && opts is string optsStr && !string.IsNullOrWhiteSpace(optsStr))
                            cg.Show(optsStr.Split('|').Select(o => o.Trim()).ToArray());
                        break;
                    case "AudioSource":
                        var aud = go.AddComponent<AudioSource>();
                        if (props.TryGetValue("clip", out var cl) && cl is string clStr && !string.IsNullOrEmpty(clStr)) aud.Clip = AssetDatabase.Load<AudioClip>(clStr);
                        if (props.TryGetValue("volume", out var vol)) aud.Volume = (float)((vol as double?) ?? 1.0);
                        if (props.TryGetValue("loop", out var lp)) aud.IsLooping = lp is true;
                        // Preview does not auto-play (BGM is triggered by scripts)
                        break;
                    case "FlashOverlay":
                        var fo = go.AddComponent<FlashOverlay>();
                        var foCol = props.GetValueOrDefault("color") as string ?? "#000000";
                        var foAlpha = (props.GetValueOrDefault("opacity") as double?) ?? 0.5;
                        fo.Color = ParseColor(foCol, (float)foAlpha);
                        break;
                    case "AdvanceIndicator":
                        go.AddComponent<AdvanceIndicator>();
                        break;
                    case "LineRenderer":
                        var lr = go.AddComponent<LineRenderer>();
                        lr.Color = ParseColor(props.GetValueOrDefault("color") as string ?? "#FFFFFF");
                        lr.Width = (float)((props.GetValueOrDefault("thickness") as double?) ?? 2.0);
                        var lx1 = (props.GetValueOrDefault("x1") as double?) ?? 0; var ly1 = (props.GetValueOrDefault("y1") as double?) ?? 0;
                        var lx2 = (props.GetValueOrDefault("x2") as double?) ?? 100; var ly2 = (props.GetValueOrDefault("y2") as double?) ?? 0;
                        lr.SetPositions(new[]
                        {
                            new Vector2((float)(lx1 / RefWidth), (float)(ly1 / RefHeight)),
                            new Vector2((float)(lx2 / RefWidth), (float)(ly2 / RefHeight)),
                        });
                        break;
                    case "HintRenderer":
                    case "SDFTextRenderer":
                        // No engine counterpart yet; skipped in preview
                        break;
                }
            }

            go.Transform.X = (float)(obj.X / RefWidth);
            go.Transform.Y = (float)(obj.Y / RefHeight);
            go.Transform.SortingOrder = obj.SortingOrder;
            go.Transform.Opacity = (float)obj.Opacity;
        }

        foreach (var obj in objects.Where(o => o.ParentId != null))
        {
            if (map.TryGetValue(obj.ParentId!, out var parent))
                map[obj.Id].Transform.SetParent(parent.Transform);
        }

        // Start (Awake was already fired by Scene.AddObject)
        host.StartScene();
    }

    private static void EmitSpriteRenderer(GameObject go, Dictionary<string, object?> props, SceneObject obj)
    {
        var sr = go.AddComponent<SpriteRenderer>();
        if (props.TryGetValue("sprite", out var sp) && sp is string sPath && !string.IsNullOrEmpty(sPath))
        {
            var native = AssetCache.GetNativeImageSize(sPath);
            float ppu = 100f;
            if (native != null && obj.Width > 0) ppu = (float)(native.Value.w * 100.0 / obj.Width);
            if (Math.Abs(ppu - 100f) < 0.5f) sr.Sprite = AssetDatabase.Load<Sprite>(sPath);
            else sr.Sprite = AssetDatabase.Load<Sprite>(sPath, ppu);
        }
        if (props.TryGetValue("color", out var sc) && sc is string sCol && !string.IsNullOrEmpty(sCol) && !sCol.Equals("#FFFFFF", StringComparison.OrdinalIgnoreCase))
            sr.Tint = ParseColor(sCol);
    }

    private static Color ParseColor(string hex, float alpha = 1f)
    {
        var (r, g, b, a) = EditorUtils.ParseHex(hex);
        if (alpha < 0.999f) a = (byte)(a * Math.Clamp(alpha, 0f, 1f));
        return new Color(r, g, b, a);
    }
}
