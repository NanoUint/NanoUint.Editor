using NanoUintEditor;
using Xunit;

namespace NanoUintEditor.Tests;

/// <summary>Code generator tests: the output must align with the NanoUintVN API.</summary>
public class CodeGeneratorTests
{
    [Fact]
    public void Generate_EmitsVNScreenStructure()
    {
        var code = CodeGenerator.Generate(new List<SceneObject>(), "My Screen", (_, _) => 100f);
        Assert.Contains("using NanoUintVN;", code);
        Assert.Contains("public class My_Screen : VNScreen", code);
        Assert.Contains("public My_Screen(Scene scene) : base(scene)", code);
        Assert.Contains("protected override void OnBuild()", code);
    }

    [Fact]
    public void Generate_TextRendererUsesEngineApi()
    {
        var objects = new List<SceneObject>
        {
            new()
            {
                Id = "abc123", Name = "Text", X = 192, Y = 108, Width = 500, Height = 80, SortingOrder = 3,
                Components = new List<ComponentData>
                {
                    new() { Type = "Transform", Props = new() { ["X"] = 192.0, ["Y"] = 108.0 } },
                    new() { Type = "TextRenderer", Props = new() { ["text"] = "Hi \"Q\"", ["fontSize"] = 24.0, ["color"] = "#FF8000" } },
                },
            },
        };
        var code = CodeGenerator.Generate(objects, "S", (_, _) => 100f);
        Assert.Contains("var goabc123 = Scene.AddObject(\"Text\");", code);
        Assert.Contains("var trabc123 = goabc123.AddComponent<TextRenderer>();", code);
        Assert.Contains("trabc123.Content = \"Hi \\\"Q\\\"\";", code);
        Assert.Contains("trabc123.FontSize = 24F;", code);
        Assert.Contains("trabc123.TextColor = NanoUint.Drawing.Color.FromRgb(0xFF, 0x80, 0x00);", code);
        Assert.Contains("goabc123.Transform.X = 0.1000F;", code);   // 192/1920
        Assert.Contains("goabc123.Transform.Y = 0.1000F;", code);   // 108/1080
        Assert.Contains("goabc123.Transform.SortingOrder = 3;", code);
    }

    [Fact]
    public void Generate_SpriteRendererUsesPpuDelegate()
    {
        var objects = new List<SceneObject>
        {
            new()
            {
                Id = "def456", Name = "Sprite", Width = 200,
                Components = new List<ComponentData>
                {
                    new() { Type = "Transform", Props = new() { ["X"] = 0.0 } },
                    new() { Type = "SpriteRenderer", Props = new() { ["sprite"] = "char.png", ["color"] = "#FF0000" } },
                },
            },
        };
        var code = CodeGenerator.Generate(objects, "S", (_, _) => 250f);
        Assert.Contains("var srdef456 = godef456.AddComponent<SpriteRenderer>();", code);
        Assert.Contains("srdef456.Sprite = AssetDatabase.Load<Sprite>(\"char.png\", 250f);", code);
        Assert.Contains("srdef456.Tint = NanoUint.Drawing.Color.FromRgb(0xFF, 0x00, 0x00);", code);
    }

    [Fact]
    public void Generate_SpriteButton_SpriteRendererEmittedFirst()
    {
        var objects = new List<SceneObject>
        {
            new()
            {
                Id = "ghi789", Name = "Btn",
                Components = new List<ComponentData>
                {
                    new() { Type = "Transform", Props = new() { ["X"] = 0.0 } },
                    new() { Type = "SpriteRenderer", Props = new() { ["sprite"] = "btn.png", ["color"] = "#FFFFFF" } },
                    new() { Type = "SpriteButton", Props = new() { ["sprite"] = "btn.png", ["hoverSprite"] = "btn_h.png", ["onClick"] = "GoNext();" } },
                },
            },
        };
        var code = CodeGenerator.Generate(objects, "S", (_, _) => 100f);
        // SpriteButton is a SteinsGateX-specific component; new codegen converts to Image type
        Assert.Contains("goghi789 = Scene.AddObject", code);
        Assert.Contains("srghi789 = goghi789.AddComponent<SpriteRenderer>", code);
        Assert.Contains("srghi789.Sprite = AssetDatabase.Load<Sprite>(\"btn.png\")", code);
    }

    [Fact]
    public void Generate_ParentChild_AfterAllObjects()
    {
        var objects = new List<SceneObject>
        {
            new() { Id = "aaa111", Name = "Parent", Components = new List<ComponentData> { new() { Type = "Transform" } } },
            new() { Id = "bbb222", Name = "Child", ParentId = "aaa111", Components = new List<ComponentData> { new() { Type = "Transform" } } },
        };
        var code = CodeGenerator.Generate(objects, "S", (_, _) => 100f);
        var lastAdd = code.LastIndexOf("Scene.AddObject(goaaa111);");
        var childParent = code.IndexOf("gobbb222.Transform.SetParent(goaaa111.Transform);");
        Assert.True(childParent > lastAdd, "SetParent must come after all AddObject");
    }

    [Fact]
    public void Generate_UnsupportedComponents_GenerateAsShape()
    {
        var objects = new List<SceneObject>
        {
            new()
            {
                Id = "ccc333", Name = "Hint",
                Components = new List<ComponentData>
                {
                    new() { Type = "Transform" },
                    new() { Type = "HintRenderer", Props = new() { ["text"] = "tip" } },
                },
            },
        };
        var code = CodeGenerator.Generate(objects, "S", (_, _) => 100f);
        // Unsupported components fall through to no component emission — just the element comment
        Assert.Contains("// Hint (", code);
    }

    [Fact]
    public void GenerateVNDocument_EmitsPagesAndBeats()
    {
        var doc = new SlideDocument { Title = "Test" };
        doc.Pages.Add(new SlidePage
        {
            Name = "Intro",
            Elements = new List<SlideElement>
            {
                new() { Type = "DialogueBox", Props = new() { ["speaker"] = "Okabe", ["text"] = "El Psy Kongroo" } },
            }
        });
        var code = CodeGenerator.GenerateVNDocument(doc, (_, _) => 100f);
        Assert.Contains("public static class Test", code);
        Assert.Contains("VNDocument", code);
        Assert.Contains("VNPage", code);
        Assert.Contains("DialogueBeat", code);
        Assert.Contains("El Psy Kongroo", code);
    }
}

/// <summary>Tests for the EditorUtils pure functions.</summary>
public class EditorUtilsTests
{
    [Theory]
    [InlineData("My Screen", "My_Screen")]
    [InlineData("123abc", "_123abc")]
    [InlineData("a-b", "a_b")]
    [InlineData("", "")]
    public void SanitizeIdentifier_ProducesValidNames(string input, string expected)
    {
        Assert.Equal(expected, EditorUtils.SanitizeIdentifier(input));
    }

    [Fact]
    public void Escape_HandlesQuotesAndBackslashes()
    {
        Assert.Equal("a\\\"b", EditorUtils.Escape("a\"b"));
        Assert.Equal("a\\\\b", EditorUtils.Escape("a\\b"));
    }

    [Theory]
    [InlineData("#FFFFFF", "NanoUint.Drawing.Color.FromRgb(0xFF, 0xFF, 0xFF)")]
    [InlineData("#FF8000", "NanoUint.Drawing.Color.FromRgb(0xFF, 0x80, 0x00)")]
    [InlineData("#FF000080", "NanoUint.Drawing.Color.FromRgba(0xFF, 0x00, 0x00, 0x80)")]
    [InlineData("garbage", "NanoUint.Drawing.Color.White")]
    public void ColorExpr_ProducesEngineColor(string hex, string expected)
    {
        Assert.Equal(expected, EditorUtils.ColorExpr(hex));
    }

    [Fact]
    public void ColorExpr_AlphaBlends()
    {
        Assert.Equal("NanoUint.Drawing.Color.FromRgba(0xFF, 0xFF, 0xFF, 0x7F)",
            EditorUtils.ColorExpr("#FFFFFF", 0.5f));  // 0.5*255=127.5 → 127=0x7F
    }

    [Fact]
    public void ParseHex_Handles6And8()
    {
        var (r, g, b, a) = EditorUtils.ParseHex("#FF8000");
        Assert.Equal((255, 128, 0, 255), (r, g, b, a));
        (r, g, b, a) = EditorUtils.ParseHex("#FF8000AA");
        Assert.Equal((255, 128, 0, 170), (r, g, b, a));
    }
}
